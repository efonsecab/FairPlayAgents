using System.ComponentModel.DataAnnotations;

namespace FairPlayAgents.Services.Configuration
{
    public record AzureOpenAIConfiguration
    {
        [Required]
        public required string? Endpoint { get; set; }
        [Required]
        public required string? DeploymentName { get; set; }
    }
}
