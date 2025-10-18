using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace FairPlayAgents.Services.AzureVideoIndexer
{
    public record VideoModel
    {
        [Required]
        public required string? VideoId { get; set; }
        public required string? Name { get; set; }
    }
}
