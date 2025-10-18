using FairPlayAgents.Services;
using System;
using System.Collections.Generic;
using System.Text;
using FairPlayAgents.Services.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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
            VideoAgentService videoAgentService = new VideoAgentService(azureOpenAIConfiguration, logger);
            string request = "Give me the list of videos.";
            string response = await videoAgentService.ProcessVideoRequest(request);
            Assert.IsFalse(string.IsNullOrEmpty(response), "Response should not be null or empty.");
        }
    }
}
