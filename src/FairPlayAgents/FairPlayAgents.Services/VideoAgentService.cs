using Azure.AI.OpenAI;
using Azure.Identity;
using FairPlayAgents.Services.AzureVideoIndexer;
using FairPlayAgents.Services.Configuration;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace FairPlayAgents.Services
{
    public class VideoAgentService
    {
        private readonly AIAgent? agent;
        private readonly ILogger<VideoAgentService> logger;

        public VideoAgentService(
            AzureOpenAIConfiguration azureOpenAIConfiguration,
            ILogger<VideoAgentService> logger)
        {
            AIAgent agent = new AzureOpenAIClient(
                new Uri(azureOpenAIConfiguration.Endpoint!),
                new AzureCliCredential())
                .GetChatClient(azureOpenAIConfiguration.DeploymentName)
                .CreateAIAgent(instructions: "You will help users with their videos.", name: nameof(VideoAgentService)
                , tools: [AIFunctionFactory.Create(GetVideoIds)])
                .AsBuilder()
                .UseOpenTelemetry()
                .Build();
            this.agent = agent;
            this.logger = logger;
        }

        public async Task<string> ProcessVideoRequest(string request)
        {
            logger.LogInformation("Processing video request: {Request}", request);
            if (agent == null)
            {
                throw new InvalidOperationException("AI Agent is not initialized.");
            }

            logger.LogInformation("AI Agent initialized successfully.");

            ActivitySource activitySource = new ActivitySource("*Microsoft.Agents.AI");
            Meter meter = new Meter("*Microsoft.Agents.AI");
            using var activity = activitySource.StartActivity("Agent Interaction");
            activity?
                .SetTag("user.input", request)
                .SetTag("agent.name", nameof(VideoAgentService));

            var response = await agent.RunAsync(request);
            
            var responseString = response.ToString();
            activity?.SetTag("agent.response", responseString);

            logger.LogInformation("Received response from AI Agent: {Response}", responseString);
            
            return responseString;
        }

        [Description("Gets a list of Video Ids")]
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
