var builder = DistributedApplication.CreateBuilder(args);

var azureOpenAIEndpoint = builder.Configuration["AzureOpenAIConfiguration:Endpoint"]!;
var azureOpenAIDeploymentName = builder.Configuration["AzureOpenAIConfiguration:DeploymentName"]!;

var apiService = builder.AddProject<Projects.FairPlayAgents_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithEnvironment(callback => 
    {
        callback.EnvironmentVariables.Add("AzureOpenAIConfiguration:Endpoint", azureOpenAIEndpoint);
        callback.EnvironmentVariables.Add("AzureOpenAIConfiguration:DeploymentName", azureOpenAIDeploymentName);
    });

builder.AddProject<Projects.FairPlayAgents_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
