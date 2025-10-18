using FairPlayAgents.Services.AzureVideoIndexer;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FairPlayAgents.ApiService.MCPTools
{
    [McpServerToolType]
    public class VideoTool
    {
        [McpServerTool, Description("Gets a list of Video Ids")]
        static IList<VideoModel> GetVideoIds()
        {
            // Placeholder for actual implementation to fetch video IDs
            var videosList = new List<VideoModel> {
            new VideoModel { VideoId = "video1", Name = "Sample Video 1" },
            new VideoModel { VideoId = "video2", Name = "Sample Video 2" },
            new VideoModel { VideoId = "video3", Name = "Sample Video 3" }
            };
            return videosList;
        }
    }
}
