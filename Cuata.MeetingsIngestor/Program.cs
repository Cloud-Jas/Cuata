using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Cuata.MeetingsIngestor.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Graph;

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
    })
    .Build();

host.Run();
