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

namespace FairPlayAgents.Services.AzureVideoIndexer
{
    public interface IAzureVideoIndexerService
    {
        string GetIndexerInfo();
        Task<string> UploadVideoFromUrlAsync(string videoUrl, string accessToken, CancellationToken cancellationToken = default);
        Task<string> GetArmAccessTokenAsync(CancellationToken cancellationToken = default);
        Task<string> UploadVideoFromUrlUsingArmAsync(string videoUrl, CancellationToken cancellationToken = default);
    }

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

                var requestUri = new Uri($"https://api.videoindexer.ai/{configuration.Location}/Accounts/{configuration.AccountId}/Videos?name={Uri.EscapeDataString(name)}&videoUrl={Uri.EscapeDataString(videoUrl)}&privacy=Private");

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
    }
}
