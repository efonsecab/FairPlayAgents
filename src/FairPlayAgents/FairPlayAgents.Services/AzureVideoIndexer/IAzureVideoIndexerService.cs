namespace FairPlayAgents.Services.AzureVideoIndexer
{
    public interface IAzureVideoIndexerService
    {
        string GetIndexerInfo();
        Task<string> UploadVideoFromUrlAsync(string videoUrl, string accessToken, CancellationToken cancellationToken = default);
        Task<string> GetArmAccessTokenAsync(CancellationToken cancellationToken = default);
        Task<string> UploadVideoFromUrlUsingArmAsync(string videoUrl, CancellationToken cancellationToken = default);
        Task<string> ListVideosAsync(CancellationToken cancellationToken = default);
        string GetVideoPublicUrl(string videoId);
    }
}
