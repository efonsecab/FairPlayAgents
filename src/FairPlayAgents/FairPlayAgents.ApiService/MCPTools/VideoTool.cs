using FairPlayAgents.Services.AzureVideoIndexer;
using ModelContextProtocol.Server;
using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace FairPlayAgents.ApiService.MCPTools
{
    [McpServerToolType]
    public class VideoTool
    {
        [McpServerTool, Description("Uploads a video from a publicly accessible URL to Azure Video Indexer")]
        public static async Task<string> UploadVideoFromUrl(string videoUrl, [FromServices] IAzureVideoIndexerService videoIndexer, [FromServices] ILogger<VideoTool> logger)
        {
            if (string.IsNullOrWhiteSpace(videoUrl))
            {
                var err = new { success = false, error = "videoUrl must be provided" };
                return JsonSerializer.Serialize(err);
            }

            try
            {
                var result = await videoIndexer.UploadVideoFromUrlUsingArmAsync(videoUrl);

                // Attempt to parse JSON response from Video Indexer and extract useful fields
                try
                {
                    using var doc = JsonDocument.Parse(result);
                    var root = doc.RootElement;

                    string? id = TryGetString(root, "id") ?? TryGetString(root, "videoId");
                    string? state = TryGetString(root, "state") ?? TryGetString(root, "status");
                    string? message = TryGetString(root, "message") ?? TryGetString(root, "error");

                    string? progress = null;
                    if (root.TryGetProperty("processingProgress", out var progressProp))
                    {
                        progress = progressProp.GetString();
                    }
                    else if (root.TryGetProperty("indexingProgress", out var indexingProp))
                    {
                        progress = indexingProp.GetString();
                    }

                    var details = new
                    {
                        success = true,
                        id,
                        state,
                        progress,
                        message,
                        raw = result
                    };

                    // If message or raw indicates the video already exists, add a hint
                    var rawLower = result?.ToLowerInvariant() ?? string.Empty;
                    if (rawLower.Contains("already") || rawLower.Contains("exists") || rawLower.Contains("reindex"))
                    {
                        // Suggest how to get indexing progress if id is present
                        if (!string.IsNullOrWhiteSpace(id))
                        {
                            var hint = new { note = "Video may already exist. You can poll the Video Indexer Index API for progress: /Accounts/{accountId}/Videos/{videoId}/Index" };
                            // merge details and hint by creating an anonymous object
                            var outObj = new { success = true, id, state, progress, message, hint, raw = result };
                            return JsonSerializer.Serialize(outObj);
                        }
                    }

                    return JsonSerializer.Serialize(details);
                }
                catch (JsonException)
                {
                    // Not a JSON response: return raw text with success flag
                    var outObj = new { success = true, raw = result };
                    return JsonSerializer.Serialize(outObj);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "UploadTool.UploadVideoFromUrl failed for {VideoUrl}", videoUrl);

                // Try to include nested exception messages if available
                var inner = ex.InnerException?.Message;
                var errorObj = new
                {
                    success = false,
                    error = ex.Message,
                    inner,
                    exceptionType = ex.GetType().FullName,
                    stackTrace = ex.StackTrace
                };

                return JsonSerializer.Serialize(errorObj);
            }
        }

        [McpServerTool, Description("Lists videos in the Azure Video Indexer account.")]
        public static async Task<string> ListVideos([FromServices] IAzureVideoIndexerService videoIndexer, [FromServices] ILogger<VideoTool> logger)
        {
            try
            {
                var result = await videoIndexer.ListVideosAsync();
                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "VideoTool.ListVideos failed.");
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
