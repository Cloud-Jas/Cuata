using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Cuata.MeetingsIngestor.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Graph;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Configuration;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
     .AddApplicationInsightsTelemetryWorkerService()
     .ConfigureFunctionsApplicationInsights();

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((ctx, services) =>
    {
       var cfg = ctx.Configuration;

       services.AddSingleton(new CosmosClient(cfg["CosmosDbConnectionString"]));
       services.AddSingleton(s =>
           new CosmosDbService(
               s.GetRequiredService<CosmosClient>(),
               cfg["CosmosDbDatabaseName"],
               cfg["CosmosDbContainerName"]
           ));

       services.AddSingleton(s =>
           new CosmosMeetingSummaryService(
               s.GetRequiredService<CosmosClient>(),
               cfg["CosmosDbDatabaseName"],
               cfg["CosmosDbMeetingSummaryContainerName"]
           ));

       services.AddSingleton(new ServiceBusClient(cfg["ServiceBusConnectionString"]));
       services.AddSingleton(s =>
           new ServiceBusPublisher(
               s.GetRequiredService<ServiceBusClient>(),
               cfg["ServiceBusQueueName"]
           ));

       var scopes = new[] { "https://graph.microsoft.com/.default" };

       var credential = new ClientSecretCredential(
           tenantId: cfg["TenantId"],
           clientId: cfg["ClientId"],
           clientSecret: cfg["ClientSecret"]);

       services.AddSingleton<GraphServiceClient>(sp => {
          return new GraphServiceClient(credential);
       });

       services.AddSingleton<GraphService>();

       var credentialOptions = new DefaultAzureCredentialOptions
       {
          TenantId = cfg.GetValue<string>("TenantId")
       };

       services.AddScoped<Kernel>(provider =>
       {
          var builder = Kernel.CreateBuilder();

          builder.AddAzureOpenAIChatCompletion(deploymentName: cfg!.GetValue<string>("OpenAIChatCompletionDeploymentName")!,
               credentials: new DefaultAzureCredential(credentialOptions),
               endpoint: cfg!.GetValue<string>("OpenAIEndpoint")!);
          return builder.Build();
       });

       services.AddSingleton<IChatCompletionService, AzureOpenAIChatCompletionService>(provider =>
       {
          return new AzureOpenAIChatCompletionService(
               deploymentName: cfg!.GetValue<string>("OpenAIChatCompletionDeploymentName")!,
               credentials: new DefaultAzureCredential(credentialOptions),
               endpoint: cfg!.GetValue<string>("OpenAIEndpoint")!
           );
       });

       services.AddSingleton<IKernelService, KernelService>();
    })
    .Build();

host.Run();
