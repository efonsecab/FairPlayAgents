using FairPlayAgents.ApiService.MCPTools;
using FairPlayAgents.Services;
using FairPlayAgents.Services.Configuration;
using FairPlayAgents.Services.AzureVideoIndexer;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddMcpServer()
    .WithTools<VideoTool>()
    .WithTools<VideoTool>()
    .WithHttpTransport();

var azureOpenAIConfiguration =
                builder.Configuration.GetSection(nameof(AzureOpenAIConfiguration))
                .Get<AzureOpenAIConfiguration>();

builder.Services.AddSingleton<AzureOpenAIConfiguration>(azureOpenAIConfiguration!);

// Bind Azure Video Indexer configuration and register for DI
var azureVideoIndexerConfiguration = builder.Configuration.GetSection(nameof(AzureVideoIndexerConfiguration))
    .Get<AzureVideoIndexerConfiguration>();

builder.Services.AddSingleton<AzureVideoIndexerConfiguration>(azureVideoIndexerConfiguration!);

// Register AzureVideoIndexerService with an HttpClient injected
builder.Services.AddHttpClient<AzureVideoIndexerService>()
    // Optionally set base address for Video Indexer API using configured location
    .ConfigureHttpClient((sp, client) =>
    {
        var cfg = sp.GetRequiredService<AzureVideoIndexerConfiguration>();
        if (!string.IsNullOrWhiteSpace(cfg.Location))
        {
            client.BaseAddress = new Uri($"https://api.videoindexer.ai/{cfg.Location}");
        }
    });

// Register the interface mapping to the concrete service using a factory that resolves the HttpClient from DI
builder.Services.AddSingleton<IAzureVideoIndexerService>(sp =>
{
    var cfg = sp.GetRequiredService<AzureVideoIndexerConfiguration>();
    var logger = sp.GetRequiredService<ILogger<AzureVideoIndexerService>>();
    var httpClientFactory = sp.GetRequiredService<System.Net.Http.IHttpClientFactory>();
    var client = httpClientFactory.CreateClient(typeof(AzureVideoIndexerService).FullName!);
    return new AzureVideoIndexerService(cfg, logger, client);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapDefaultEndpoints();
app.MapMcp();

app.Run();
