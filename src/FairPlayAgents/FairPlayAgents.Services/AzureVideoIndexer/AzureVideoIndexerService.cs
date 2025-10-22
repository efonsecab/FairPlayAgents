using FairPlayAgents.Services.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System;
using Azure.Core;
using Azure.Identity;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Collections.Generic;
using System.Linq;

namespace FairPlayAgents.Services.AzureVideoIndexer
{

    public class AzureVideoIndexerService : IAzureVideoIndexerService
    {
        private readonly AzureVideoIndexerConfiguration configuration;
        private readonly ILogger<AzureVideoIndexerService> logger;
        private readonly HttpClient httpClient;

        public AzureVideoIndexerService(AzureVideoIndexerConfiguration configuration, ILogger<AzureVideoIndexerService> logger, HttpClient httpClient)
        {
            this.configuration = configuration;
            this.logger = logger;
            this.httpClient = httpClient;

            logger.LogInformation("AzureVideoIndexerService initialized with AccountId: {AccountId}, Location: {Location}", configuration.AccountId, configuration.Location);
        }

        public string GetIndexerInfo()
        {
            return $"AccountId={configuration.AccountId}; Location={configuration.Location}; Resource={configuration.ResourceName}";
        }

        // Build the Video Indexer player URL using the accountId, videoId and location.
        // Use the URL shape you provided: https://www.videoindexer.ai/accounts/{accountId}/videos/{videoId}/?location={location}
        public string GetVideoPublicUrl(string videoId)
        {
            if (string.IsNullOrWhiteSpace(videoId) || string.IsNullOrWhiteSpace(configuration.Location) || string.IsNullOrWhiteSpace(configuration.AccountId))
            {
                return string.Empty;
            }

            return $"https://www.videoindexer.ai/accounts/{configuration.AccountId}/videos/{Uri.EscapeDataString(videoId)}/?location={Uri.EscapeDataString(configuration.Location)}";
        }

        /// <summary>
        /// Obtains an ARM access token from Azure AD using DefaultAzureCredential.
        /// The token audience/scope used is https://management.azure.com/.default
        /// </summary>
        public async Task<string> GetArmAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            if (!configuration.IsArmAccount)
            {
                throw new InvalidOperationException("Configuration indicates this is not an ARM-based Video Indexer account.");
            }

            if (string.IsNullOrWhiteSpace(configuration.SubscriptionId) || string.IsNullOrWhiteSpace(configuration.ResourceGroup) || string.IsNullOrWhiteSpace(configuration.ResourceName))
            {
                throw new InvalidOperationException("AzureVideoIndexerConfiguration is missing required ARM resource identifiers (SubscriptionId, ResourceGroup, ResourceName).");
            }

            try
            {
                var credential = new DefaultAzureCredential();
                var tokenRequestContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
                var accessToken = await credential.GetTokenAsync(tokenRequestContext, cancellationToken).ConfigureAwait(false);
                logger.LogInformation("Acquired ARM access token (expires at {ExpiresOn})", accessToken.ExpiresOn);
                return accessToken.Token;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to acquire ARM access token.");
                throw;
            }
        }

        /// <summary>
        /// Exchanges an ARM management access token for a Video Indexer account access token by calling the ARM management generateAccessToken endpoint.
        /// </summary>
        private async Task<string> ExchangeArmTokenForAccountTokenAsync(string armToken, CancellationToken cancellationToken = default)
        {
            var apiVersion = string.IsNullOrWhiteSpace(configuration.ApiVersion) ? "2024-01-01" : configuration.ApiVersion;
            var url = $"https://management.azure.com/subscriptions/{configuration.SubscriptionId}/resourceGroups/{configuration.ResourceGroup}/providers/Microsoft.VideoIndexer/accounts/{configuration.ResourceName}/generateAccessToken?api-version={apiVersion}";

            var requestBody = new { PermissionType = "Contributor", Scope = "Account" };

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", armToken);
            req.Content = JsonContent.Create(requestBody);

            var resp = await httpClient.SendAsync(req, cancellationToken).ConfigureAwait(false);
            var content = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("ExchangeArmTokenForAccountTokenAsync failed. Status: {Status}, Response: {Response}", resp.StatusCode, content);
                resp.EnsureSuccessStatusCode();
            }

            try
            {
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("accessToken", out var tokenProp) && tokenProp.ValueKind == JsonValueKind.String)
                {
                    return tokenProp.GetString()!;
                }

                // sometimes response shape may include 'value' object with accessToken
                if (doc.RootElement.TryGetProperty("value", out var valueProp) && valueProp.ValueKind == JsonValueKind.Object && valueProp.TryGetProperty("accessToken", out var nested) && nested.ValueKind == JsonValueKind.String)
                {
                    return nested.GetString()!;
                }

                throw new InvalidOperationException("Unexpected token response shape from ARM generateAccessToken endpoint: " + content);
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Failed to parse token exchange response: {Response}", content);
                throw;
            }
        }

        /// <summary>
        /// Uploads a video using an ARM-based token. This helper will request an ARM access token, exchange it for a Video Indexer account token, and then call the upload endpoint with that account token.
        /// </summary>
        public async Task<string> UploadVideoFromUrlUsingArmAsync(string videoUrl, CancellationToken cancellationToken = default)
        {
            if (!configuration.IsArmAccount)
            {
                throw new InvalidOperationException("Configuration indicates this is not an ARM-based Video Indexer account.");
            }

            var armToken = await GetArmAccessTokenAsync(cancellationToken).ConfigureAwait(false);

            // Exchange ARM management token for a Video Indexer account token via the ARM management API
            var accountToken = await ExchangeArmTokenForAccountTokenAsync(armToken, cancellationToken).ConfigureAwait(false);

            // Use the account token to call the Video Indexer upload endpoint
            return await UploadVideoFromUrlAsync(videoUrl, accountToken, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Uploads a video to Azure Video Indexer by providing a publicly accessible video URL.
        /// Requires a valid account access token for the Video Indexer API (bearer token).
        /// The returned string is the raw API response (if JSON) — callers (MCP tools) can parse it and use GetVideoPublicUrl to obtain a clickable link.
        /// </summary>
        public async Task<string> UploadVideoFromUrlAsync(string videoUrl, string accessToken, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(videoUrl))
            {
                throw new ArgumentException("videoUrl must be provided", nameof(videoUrl));
            }

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new ArgumentException("accessToken must be provided", nameof(accessToken));
            }

            if (string.IsNullOrWhiteSpace(configuration.Location) || string.IsNullOrWhiteSpace(configuration.AccountId))
            {
                throw new InvalidOperationException("AzureVideoIndexerConfiguration is missing required values (Location and AccountId).");
            }

            try
            {
                var name = "uploaded-video";

                var requestUri = new Uri($"https://api.videoindexer.ai/{configuration.Location}/Accounts/{configuration.AccountId}/Videos?name={Uri.EscapeDataString(name)}&videoUrl={Uri.EscapeDataString(videoUrl)}&privacy=Public&preventDuplicates=false");

                // Set Authorization header on HttpClient
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                // For URL uploads the API accepts a POST with empty body/null content
                var response = await httpClient.PostAsync(requestUri, null, cancellationToken).ConfigureAwait(false);
                var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("UploadVideoFromUrlAsync failed. Status: {Status}, Response: {Response}", response.StatusCode, content);
                    response.EnsureSuccessStatusCode(); // will throw
                }

                logger.LogInformation("Video upload initiated. Response: {Response}", content);
                return content;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error uploading video from url: {Url}", videoUrl);
                throw;
            }
        }

        /// <summary>
        /// Lists videos for the configured ARM-based Video Indexer account.
        /// Returns JSON string: { success: true, videos: [ { id, name, state, publicUrl, raw } ], raw = <api response> } or an error object string.
        /// </summary>
        public async Task<string> ListVideosAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (!configuration.IsArmAccount)
                {
                    return JsonSerializer.Serialize(new { success = false, error = "Configuration indicates this is not an ARM-based Video Indexer account." });
                }

                if (string.IsNullOrWhiteSpace(configuration.SubscriptionId) || string.IsNullOrWhiteSpace(configuration.ResourceGroup) || string.IsNullOrWhiteSpace(configuration.ResourceName) || string.IsNullOrWhiteSpace(configuration.Location) || string.IsNullOrWhiteSpace(configuration.AccountId))
                {
                    return JsonSerializer.Serialize(new { success = false, error = "AzureVideoIndexerConfiguration is missing required ARM or account identifiers." });
                }

                // Acquire ARM token
                var armToken = await GetArmAccessTokenAsync(cancellationToken).ConfigureAwait(false);

                // Exchange for account token
                var accountToken = await ExchangeArmTokenForAccountTokenAsync(armToken, cancellationToken).ConfigureAwait(false);

                // Call Video Indexer API to list videos. Do not request streaming URLs — we'll build player URL ourselves.
                var listUrl = $"https://api.videoindexer.ai/{configuration.Location}/Accounts/{configuration.AccountId}/Videos?skip=0&top=100";
                using var req = new HttpRequestMessage(HttpMethod.Get, listUrl);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accountToken);

                var resp = await httpClient.SendAsync(req, cancellationToken).ConfigureAwait(false);
                var content = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                if (!resp.IsSuccessStatusCode)
                {
                    logger.LogWarning("ListVideosAsync: Video Indexer list call failed. Status: {Status}, Response: {Response}", resp.StatusCode, content);
                    return JsonSerializer.Serialize(new { success = false, error = "Video Indexer list API returned failure.", status = (int)resp.StatusCode, response = content });
                }

                // Parse and augment per-video with a player URL constructed from accountId/location/videoId
                try
                {
                    using var doc = JsonDocument.Parse(content);
                    var root = doc.RootElement;

                    // locate an array of items in common response shapes
                    JsonElement videosArray = default;
                    if (root.ValueKind == JsonValueKind.Object)
                    {
                        if (root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
                        {
                            videosArray = results;
                        }
                        else if (root.TryGetProperty("videos", out var videos) && videos.ValueKind == JsonValueKind.Array)
                        {
                            videosArray = videos;
                        }
                        else if (root.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array)
                        {
                            videosArray = value;
                        }
                    }
                    else if (root.ValueKind == JsonValueKind.Array)
                    {
                        videosArray = root;
                    }

                    var list = new List<object>();

                    if (videosArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in videosArray.EnumerateArray())
                        {
                            var id = TryGetString(item, "id") ?? TryGetString(item, "videoId");
                            var name = TryGetString(item, "name") ?? TryGetString(item, "videoName") ?? TryGetString(item, "displayName");
                            var state = TryGetString(item, "state") ?? TryGetString(item, "status");

                            // Construct player URL using video id and configured location/account
                            var publicUrl = string.IsNullOrWhiteSpace(id) ? null : GetVideoPublicUrl(id);

                            list.Add(new
                            {
                                id,
                                name,
                                state,
                                publicUrl,
                                raw = item
                            });
                        }
                    }

                    return JsonSerializer.Serialize(new { success = true, videos = list, raw = root });
                }
                catch (JsonException)
                {
                    // Return raw text if parsing fails
                    return JsonSerializer.Serialize(new { success = true, raw = content });
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ListVideosAsync failed.");
                return JsonSerializer.Serialize(new { success = false, error = ex.Message, exceptionType = ex.GetType().FullName });
            }
        }

        private static string? TryGetString(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }
            return null;
        }
    }
}
