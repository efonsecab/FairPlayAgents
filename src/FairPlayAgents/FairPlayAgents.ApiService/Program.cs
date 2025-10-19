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
    .WithTools<UploadTool>()
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
    var client = httpClientFactory.CreateClient(typeof(AzureVideoIndexerService).FullName);
    return new AzureVideoIndexerService(cfg, logger, client);
});

builder.Services.AddTransient<VideoAgentService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

string[] summaries = ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.MapGet("/videos", async (string request, [FromServices] VideoAgentService videoAgentService) => 
{
    var result = await videoAgentService.ProcessVideoRequest(request);
    return result;
});

app.MapDefaultEndpoints();
app.MapMcp();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
