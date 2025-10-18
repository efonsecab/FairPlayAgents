using FairPlayAgents.ApiService.MCPTools;
using FairPlayAgents.Services;
using FairPlayAgents.Services.Configuration;
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
    .WithHttpTransport();

var azureOpenAIConfiguration =
                builder.Configuration.GetSection(nameof(AzureOpenAIConfiguration))
                .Get<AzureOpenAIConfiguration>();

builder.Services.AddSingleton<AzureOpenAIConfiguration>(azureOpenAIConfiguration!);
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
