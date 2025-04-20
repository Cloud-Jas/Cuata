using Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Newtonsoft.Json;
using OpenTelemetry.Trace;
using Cuata.Constants;
using Cuata.Handlers;
using Cuata.Models;
#pragma warning disable SKEXP0001

namespace Cuata.Plugins
{
   public class OtelFunctionCallFilter : IFunctionInvocationFilter
   {
      private readonly TracingContextCache _itemsCache;
      private readonly Tracer _tracer;

      public OtelFunctionCallFilter(IServiceProvider serviceProvider)
      {
         _itemsCache = serviceProvider.GetRequiredService<TracingContextCache>();
         _tracer = serviceProvider.GetRequiredService<TracerProvider>().GetTracer("CuataDemo");
      }
      private DateTime invocationStartTime { get; set; }
      private DateTime invocationCompletionTime { get; set; }

      public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
      {
         _itemsCache[OpenTelemetryConstants.GEN_AI_OPERATION_NAME_KEY] = "execute_tool";
         _itemsCache[OpenTelemetryConstants.GEN_AI_REQUEST_MODEL_KEY] = "gpt-4o";

         var parentSpan = _tracer.StartSpan($"{_itemsCache[OpenTelemetryConstants.GEN_AI_OPERATION_NAME_KEY]} {_itemsCache[OpenTelemetryConstants.GEN_AI_REQUEST_MODEL_KEY]}", SpanKind.Client);

         try
         {
            DateTime invocationStartTime = DateTime.UtcNow;

            parentSpan.SetAttribute("StartTime", invocationStartTime.ToLongTimeString());

            parentSpan.SetAttribute(OpenTelemetryConstants.GEN_AI_TOOL_NAME, context.Function.Name);

            await next(context);

            var userEventAttributes = new SpanAttributes();
            userEventAttributes.Add("id", context.Function.Name);
            userEventAttributes.Add("role", "tool");
            userEventAttributes.Add("content", context.Result.ToString());

            parentSpan.AddEvent(OpenTelemetryConstants.GEN_AI_TOOL_MESSAGE, userEventAttributes);

            invocationCompletionTime = DateTime.UtcNow;


         }
         catch (Exception ex)
         {
            parentSpan?.RecordException(ex);
            parentSpan?.SetAttribute(OpenTelemetryConstants.GEN_AI_ERROR_TYPE, ex.Message);
         }
         finally
         {
            parentSpan.SetAttribute("EndTime", invocationCompletionTime.ToLongTimeString());
         }

      }
   }
}
