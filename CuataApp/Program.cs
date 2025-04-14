using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Cuata;
using Cuata.Extensions;
using Cuata.Modules;
using System;
using System.Text;
using Cuata.Services;
using System.Reflection.Metadata.Ecma335;
using Microsoft.CognitiveServices.Speech;

class Program
{
   static async Task Main(string[] args)
   {
      Console.OutputEncoding = Encoding.UTF8;
      Console.Title = "👩‍💻 CUATA - Personal Assistant";

      var host = Host.CreateDefaultBuilder(args)
           .ConfigureAppConfiguration((hostingContext, config) =>
           {
              config.AddJsonFile("appsettings.json", optional: true);
              config.AddJsonFile("appsettings.Development.json", optional: false);
           })
           .ConfigureServices((context, services) =>
           {
              var configuration = context.Configuration;
              var serviceName = "OtelDemo";
              var serviceVersion = "1.0.0";

              services.AddOpenTelemetryTelemetry(configuration, serviceName, serviceVersion);

              services.AddSemanticKernel(configuration);

              services.AddTransient<ChatCompletion>();
              services.AddTransient<ChatCompletionWithFunctionCalls>();
              services.AddTransient<MultiAgentInteractions>();
              services.AddTransient<CuaAgent>();

              services.AddSingleton<ModuleFactory>();

              services.AddSingleton<CuataApp>();
              services.AddSingleton<OcrProcessorService>();
              services.AddSingleton<SpeechRecognizer>(serviceProvider =>
              {
                 var config = serviceProvider.GetRequiredService<IConfiguration>();
                 var speechKey = config["CognitiveServicesSpeechKey"];
                 var speechRegion = config["CognitiveServicesSpeechRegion"];

                 var speechConfig = SpeechConfig.FromSubscription(speechKey, speechRegion);
                 return new SpeechRecognizer(speechConfig);
              });

              services.AddHostedService<ServiceBusReceiverService>();

           })
           .Build();

      _ = host.RunAsync();

      var app = host.Services.GetRequiredService<CuataApp>();
      await app.Run();
      await host.StopAsync();

   }
}
