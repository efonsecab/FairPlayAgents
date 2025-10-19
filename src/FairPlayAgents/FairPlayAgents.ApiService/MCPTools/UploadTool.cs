using FairPlayAgents.Services.AzureVideoIndexer;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace FairPlayAgents.ApiService.MCPTools
{
    [McpServerToolType]
    public class UploadTool
    {
        [McpServerTool, Description("Uploads a video from a publicly accessible URL to Azure Video Indexer")]
        public static async Task<string> UploadVideoFromUrl(string videoUrl, [FromServices] IAzureVideoIndexerService videoIndexer)
        {
            // The tool delegates to the AzureVideoIndexer service. The service will handle token acquisition for ARM accounts when configured.
            var result = await videoIndexer.UploadVideoFromUrlUsingArmAsync(videoUrl);
            return result;
        }
    }
}
