using System;
using System.Collections.Generic;
using System.Text;

namespace FairPlayAgents.Services.Configuration
{
    public record AzureVideoIndexerConfiguration
    {
        public string? AccountId { get; set; }
        public string? Location { get; set; }
        public bool IsArmAccount { get; set; }
        public string? ResourceGroup { get; set; }
        public string? SubscriptionId { get; set; }
        public string? ResourceName { get; set; }
        public string? ApiVersion { get; set; } = "2024-01-01";
    }
}
