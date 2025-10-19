var builder = DistributedApplication.CreateBuilder(args);

var azureOpenAIEndpoint = builder.Configuration["AzureOpenAIConfiguration:Endpoint"]!;
var azureOpenAIDeploymentName = builder.Configuration["AzureOpenAIConfiguration:DeploymentName"]!;

var azureVideoIndexerAccountId = builder.Configuration["AzureVideoIndexerConfiguration:AccountId"]!;
var azureVideoIndexerLocation = builder.Configuration["AzureVideoIndexerConfiguration:Location"]!;
var azureVideoIndexerIsArmAccount = builder.Configuration["AzureVideoIndexerConfiguration:IsArmAccount"] ?? "false";
var azureVideoIndexerResourceGroup = builder.Configuration["AzureVideoIndexerConfiguration:ResourceGroup"]!;
var azureVideoIndexerSubscriptionId = builder.Configuration["AzureVideoIndexerConfiguration:SubscriptionId"]!;
var azureVideoIndexerResourceName = builder.Configuration["AzureVideoIndexerConfiguration:ResourceName"]!;
var azureVideoIndexerApiVersion = builder.Configuration["AzureVideoIndexerConfiguration:ApiVersion"]!;


var apiService = builder.AddProject<Projects.FairPlayAgents_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithEnvironment(callback => 
    {
        callback.EnvironmentVariables.Add("AzureOpenAIConfiguration:Endpoint", azureOpenAIEndpoint);
        callback.EnvironmentVariables.Add("AzureOpenAIConfiguration:DeploymentName", azureOpenAIDeploymentName);

        // Pass Azure Video Indexer configuration through environment variables so the ApiService can bind it at startup
        callback.EnvironmentVariables.Add("AzureVideoIndexerConfiguration:AccountId", azureVideoIndexerAccountId);
        callback.EnvironmentVariables.Add("AzureVideoIndexerConfiguration:Location", azureVideoIndexerLocation);
        callback.EnvironmentVariables.Add("AzureVideoIndexerConfiguration:IsArmAccount", azureVideoIndexerIsArmAccount);
        callback.EnvironmentVariables.Add("AzureVideoIndexerConfiguration:ResourceGroup", azureVideoIndexerResourceGroup);
        callback.EnvironmentVariables.Add("AzureVideoIndexerConfiguration:SubscriptionId", azureVideoIndexerSubscriptionId);
        callback.EnvironmentVariables.Add("AzureVideoIndexerConfiguration:ResourceName", azureVideoIndexerResourceName);
        callback.EnvironmentVariables.Add("AzureVideoIndexerConfiguration:ApiVersion", azureVideoIndexerApiVersion);
    });

builder.AddProject<Projects.FairPlayAgents_Web>("webfrontend")
    .WithEnvironment(callback => 
    {
        callback.EnvironmentVariables.Add("AzureOpenAIConfiguration:Endpoint", azureOpenAIEndpoint);
        callback.EnvironmentVariables.Add("AzureOpenAIConfiguration:DeploymentName", azureOpenAIDeploymentName);
    })
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
