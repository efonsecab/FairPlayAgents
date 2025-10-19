using FairPlayAgents.Services;
using System;
using System.Collections.Generic;
using System.Text;
using FairPlayAgents.Services.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using FairPlayAgents.Services.AzureVideoIndexer;
using System.Threading;
using System.Threading.Tasks;

namespace FairPlayAgents.Tests.Services
{
    [TestClass]
    public class VideoAgentServiceTests
    {

        [TestMethod]
        public async Task Test_ProcessRequestAsync()
        {
            ConfigurationBuilder configurationBuilder = new();
            configurationBuilder.AddUserSecrets<VideoAgentServiceTests>();
            var config = configurationBuilder.Build();

            var azureOpenAIConfiguration = 
                config.GetSection(nameof(AzureOpenAIConfiguration))
                .Get<AzureOpenAIConfiguration>();


            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });
            var logger = 
            loggerFactory.CreateLogger<VideoAgentService>();

            // Provide a simple stub for IAzureVideoIndexerService
            var stubVideoIndexer = new StubVideoIndexerService();

            VideoAgentService videoAgentService = new VideoAgentService(azureOpenAIConfiguration, logger, stubVideoIndexer);
            string request = "Give me the list of videos.";
            string response = await videoAgentService.ProcessVideoRequest(request);
            Assert.IsFalse(string.IsNullOrEmpty(response), "Response should not be null or empty.");
        }

        private class StubVideoIndexerService : IAzureVideoIndexerService
        {
            public Task<string> GetArmAccessTokenAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult("stub-token");
            }

            public string GetIndexerInfo()
            {
                return "stub-info";
            }

            public Task<string> UploadVideoFromUrlAsync(string videoUrl, string accessToken, CancellationToken cancellationToken = default)
            {
                return Task.FromResult($"uploaded:{videoUrl}");
            }

            public Task<string> UploadVideoFromUrlUsingArmAsync(string videoUrl, CancellationToken cancellationToken = default)
            {
                return Task.FromResult($"uploaded-arm:{videoUrl}");
            }
        }
    }
}
