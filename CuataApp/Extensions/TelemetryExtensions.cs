using Azure.Monitor.OpenTelemetry.Exporter;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Cuata.Constants;
using Cuata.Handlers;
using Cuata.Models;
using Cuata.Services;

namespace Cuata.Extensions
{
   public static class TelemetryExtensions
   {
      public static IServiceCollection AddOpenTelemetryTelemetry(this IServiceCollection services, IConfiguration configuration, string serviceName, string serviceVersion)
      {
         var openTelemetryResourceBuilder = ResourceBuilder.CreateDefault().AddService(serviceName: serviceName, serviceVersion: serviceVersion);
         var openTelemetryTracerProvider = Sdk.CreateTracerProviderBuilder()
                 .AddOtlpExporter()
                 .AddAzureMonitorTraceExporter(c => c.ConnectionString = configuration.GetValue<string>("APPLICATIONINSIGHTS_CONNECTION_STRING"))
                 .AddSource(serviceName)
                 .AddSource("Microsoft.SemanticKernel*")
                 .SetSampler(new AlwaysOnSampler())
                 .SetResourceBuilder(openTelemetryResourceBuilder)
                 .Build();

         var metricsProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter("GenAI.Client", "GenAI.Server")
            .AddMeter("Microsoft.SemanticKernel*")
            .AddConsoleExporter()
            .AddAzureMonitorMetricExporter(configure =>
            {
               configure.ConnectionString = configuration.GetValue<string>("APPLICATIONINSIGHTS_CONNECTION_STRING");              
            });

         services.AddSingleton<TracerProvider>(openTelemetryTracerProvider);
         services.AddSingleton<ILoggerProvider, OpenTelemetryLoggerProvider>();
         services.AddScoped<TracingContextCache>();
         services.AddScoped<IOtelTracingHandler,OtelTracingHandler>();
         services.Configure<TelemetryConfig>(configuration);
         services.AddSingleton<ITelemetryToggleService, TelemetryToggleService>();

         services.Configure<OpenTelemetryLoggerOptions>((openTelemetryLoggerOptions) =>
         {
            openTelemetryLoggerOptions.SetResourceBuilder(openTelemetryResourceBuilder);
            openTelemetryLoggerOptions.IncludeFormattedMessage = true;
            openTelemetryLoggerOptions.AddConsoleExporter();
            openTelemetryLoggerOptions.AddAzureMonitorLogExporter(c => c.ConnectionString = configuration.GetValue<string>("APPLICATIONINSIGHTS_CONNECTION_STRING"));
         }
         );

         services.AddOpenTelemetry(b =>
         {
            b.IncludeFormattedMessage = true;
            b.IncludeScopes = true;
            b.ParseStateValues = true;
         });
         return services;
      }
      public static IServiceCollection AddOpenTelemetry(this IServiceCollection services, Action<OpenTelemetryLoggerOptions> configure = null)
      {
         ArgumentNullException.ThrowIfNull(services);

         services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, OpenTelemetryLoggerProvider>());

         if (configure != null)
         {
            services.Configure(configure);
         }

         return services;
      }

      public static void AddOpenTelemetryHeaders(this HttpClient client, IDictionary<string, object> itemsCache)
      {
         if (itemsCache == null || itemsCache.Count == 0)
            return;

         if (itemsCache.TryGetValue(OpenTelemetryConstants.TRACEID_KEY, out var traceId))
            client.DefaultRequestHeaders.Add(OpenTelemetryConstants.TRACEID_KEY, traceId.ToString());

         if (itemsCache.TryGetValue(OpenTelemetryConstants.PARENT_SPANID_KEY, out var parentSpanId))
            client.DefaultRequestHeaders.Add(OpenTelemetryConstants.PARENT_SPANID_KEY, parentSpanId.ToString());

         if (itemsCache.TryGetValue(OpenTelemetryConstants.PARENT_SPAN_TRACEFLAG_KEY, out var parentSpanTraceFlag))
            client.DefaultRequestHeaders.Add(OpenTelemetryConstants.PARENT_SPAN_TRACEFLAG_KEY, parentSpanTraceFlag.ToString());
      }
   }

}
