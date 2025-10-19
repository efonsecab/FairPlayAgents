using Azure.AI.OpenAI;
using Azure.Identity;
using FairPlayAgents.Web;
using FairPlayAgents.Web.Components;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OpenAI;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOutputCache();

builder.Services.AddHttpClient<WeatherApiClient>(client =>
    {
        // This URL uses "https+http://" to indicate HTTPS is preferred over HTTP.
        // Learn more about service discovery scheme resolution at https://aka.ms/dotnet/sdschemes.
        client.BaseAddress = new("https+http://apiservice");
    });

// Register a named HttpClient that resolves to the ApiService via service discovery.
builder.Services.AddHttpClient("apiservice", client =>
{
    client.BaseAddress = new Uri("https+http://apiservice");
});

builder.Services.AddSingleton<McpClient>(sp =>
{
    McpClientOptions mcpClientOptions = new()
    { ClientInfo = new() { Name = "AspNetCoreSseClient", Version = "1.0.0" } };

    var client = new HttpClient();
    client.BaseAddress = new("https+http://apiservice");

    // can't use the service discovery for ["https +http://aspnetsseserver"]
    // fix: read the environment value for the key 'services__aspnetsseserver__https__0' to get the url for the aspnet core sse server
    var serviceName = "apiservice";
    var name = $"services__{serviceName}__https__0";
    var url = Environment.GetEnvironmentVariable(name) + "/sse";



    HttpClientTransportOptions httpClientTransportOptions = new HttpClientTransportOptions()
    { 
        Endpoint= new Uri(url),
        TransportMode = HttpTransportMode.Sse
    };
    HttpClientTransport httpClientTransport = new HttpClientTransport(httpClientTransportOptions);
    var mcpClient = McpClient.CreateAsync(httpClientTransport).Result;
    return mcpClient!;
});

var azureOpenAIEndpoint = builder.Configuration["AzureOpenAIConfiguration:Endpoint"]!;
var azureOpenAIDeploymentName = builder.Configuration["AzureOpenAIConfiguration:DeploymentName"]!;

builder.Services.AddKeyedTransient<AIAgent>("VideoAgent", (sp,key) => 
{
    var mcpClient = sp.GetRequiredService<McpClient>();
    var tools = mcpClient.ListToolsAsync().Result;
    AIAgent agent = new AzureOpenAIClient(
        new Uri(azureOpenAIEndpoint), new DefaultAzureCredential())
        .GetChatClient(azureOpenAIDeploymentName)
        .CreateAIAgent(instructions: "You will help users with their videos.",
        tools: [.. tools.Cast<AITool>()]);
    return agent;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.UseOutputCache();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
