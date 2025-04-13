using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Newtonsoft.Json;
using OpenTelemetry.Trace;
using Cuata.Constants;
using Cuata.Models;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Cuata.Handlers
{
   public class OtelTracingHandler : IOtelTracingHandler
   {
      private readonly Tracer _tracer;
      private readonly TracingContextCache _itemsCache;
      private readonly ILogger<OtelTracingHandler> _logger;

      public OtelTracingHandler(TracerProvider tracerProvider, TracingContextCache itemsCache, ILogger<OtelTracingHandler> logger)
      {
         _tracer = tracerProvider.GetTracer("OtelDemo");
         _itemsCache = itemsCache;
         _logger = logger;
      }
      private DateTime invocationStartTime { get; set; }
      private DateTime invocationCompletionTime { get; set; }
      public async Task<TResponse> TraceRequest<TResponse>(Func<RequestData, Task<TResponse>> runTraceRequest, RequestData requestData)
      {
         
         TelemetrySpan tempTelemetrySpan;
         if (_itemsCache.ContainsKey(OpenTelemetryConstants.TRACEID_KEY))
         {
            ActivityTraceId parentTraceIdObj = ActivityTraceId.CreateFromString(new ReadOnlySpan<char>(_itemsCache[OpenTelemetryConstants.TRACEID_KEY].ToString()?.ToCharArray()));
            ActivitySpanId parentSpanIdObj = ActivitySpanId.CreateFromString(new ReadOnlySpan<char>(_itemsCache[OpenTelemetryConstants.PARENT_SPANID_KEY].ToString()?.ToCharArray()));
            ActivityTraceFlags activityTraceFlags;
            bool parseResult = Enum.TryParse<ActivityTraceFlags>(_itemsCache[OpenTelemetryConstants.PARENT_SPAN_TRACEFLAG_KEY].ToString(), out activityTraceFlags);
            tempTelemetrySpan = _tracer.StartActiveSpan($"{_itemsCache[OpenTelemetryConstants.GEN_AI_OPERATION_NAME_KEY]} {_itemsCache[OpenTelemetryConstants.GEN_AI_REQUEST_MODEL_KEY]}", SpanKind.Client, new SpanContext(parentTraceIdObj, parentSpanIdObj, activityTraceFlags));
         }
         else
         {
            tempTelemetrySpan = _tracer.StartActiveSpan($"{_itemsCache[OpenTelemetryConstants.GEN_AI_OPERATION_NAME_KEY]} {_itemsCache[OpenTelemetryConstants.GEN_AI_REQUEST_MODEL_KEY]}", SpanKind.Client);
         }
         using var parentSpan = tempTelemetrySpan;

         try
         {
            if (!_itemsCache.ContainsKey(OpenTelemetryConstants.TRACEID_KEY))
            {
               _itemsCache.Add(OpenTelemetryConstants.TRACEID_KEY, parentSpan.Context.TraceId.ToString());
               _itemsCache.Add(OpenTelemetryConstants.PARENT_SPANID_KEY, parentSpan.Context.SpanId.ToString());
               _itemsCache.Add(OpenTelemetryConstants.PARENT_SPAN_TRACEFLAG_KEY, parentSpan.Context.TraceFlags.ToString());
            }
            DateTime invocationStartTime = DateTime.UtcNow;

            parentSpan.SetAttribute("StartTime", invocationStartTime.ToLongTimeString());

            var userEventAttributes = new SpanAttributes();
            userEventAttributes.Add("role", "user");
            userEventAttributes.Add("content", requestData.UserQuery ?? "");

            parentSpan.AddEvent(OpenTelemetryConstants.GEN_AI_USER_MESSAGE, userEventAttributes);

            var response = await runTraceRequest(requestData);

            if (response is ChatMessageContent)
            {
               var chatMessageContent = (ChatMessageContent)(object)response;

               parentSpan.SetAttribute(OpenTelemetryConstants.GEN_AI_USAGE_INPUT_TOKENS_KEY, (chatMessageContent.InnerContent as OpenAI.Chat.ChatCompletion)!.Usage.InputTokenCount);
               parentSpan.SetAttribute(OpenTelemetryConstants.GEN_AI_USAGE_OUTPUT_TOKENS_KEY, (chatMessageContent.InnerContent as OpenAI.Chat.ChatCompletion)!.Usage.OutputTokenCount);
               parentSpan.SetAttribute(OpenTelemetryConstants.GEN_AI_REQUEST_MAX_TOKENS_KEY, (chatMessageContent.InnerContent as OpenAI.Chat.ChatCompletion)!.Usage.TotalTokenCount);
               parentSpan.SetAttribute(OpenTelemetryConstants.GEN_AI_RESPONSE_FINISH_REASONS_KEY, (chatMessageContent.InnerContent as OpenAI.Chat.ChatCompletion)!.FinishReason.ToString());
               parentSpan.SetAttribute(OpenTelemetryConstants.GEN_AI_RESPONSE_ID_KEY, (chatMessageContent.InnerContent as OpenAI.Chat.ChatCompletion)!.Id.ToString());
               parentSpan.SetAttribute(OpenTelemetryConstants.GEN_AI_RESPONSE_MODEL_KEY, (chatMessageContent.InnerContent as OpenAI.Chat.ChatCompletion)!.Model.ToString());
               if (_itemsCache.ContainsKey(OpenTelemetryConstants.GEN_AI_REQUEST_TOP_P_KEY))
               {
                  parentSpan.SetAttribute(OpenTelemetryConstants.GEN_AI_REQUEST_TOP_P_KEY, _itemsCache[OpenTelemetryConstants.GEN_AI_REQUEST_TOP_P_KEY].ToString());
               }
               if (_itemsCache.ContainsKey(OpenTelemetryConstants.GEN_AI_REQUEST_TEMPERATURE_KEY))
               {
                  parentSpan.SetAttribute(OpenTelemetryConstants.GEN_AI_REQUEST_TEMPERATURE_KEY, _itemsCache[OpenTelemetryConstants.GEN_AI_REQUEST_TEMPERATURE_KEY].ToString());
               }

               var assistantEventAttributes = new SpanAttributes();
               assistantEventAttributes.Add("role", "assistant");
               assistantEventAttributes.Add("content", chatMessageContent.Content ?? "");
               parentSpan.AddEvent(OpenTelemetryConstants.GEN_AI_ASSISTANT_MESSAGE, assistantEventAttributes);

            }

            invocationCompletionTime = DateTime.UtcNow;

            return response;

         }
         catch (Exception ex)
         {
            parentSpan?.RecordException(ex);
            parentSpan?.SetAttribute(OpenTelemetryConstants.GEN_AI_ERROR_TYPE, ex.Message);
            var errorMessage = $"Error occurred while processing the request: {ex.Message}";
            var errorDetails = JsonConvert.SerializeObject(ex, Formatting.Indented);
            var errorResponse = new ErrorResponse
            {
               Message = errorMessage,
               Details = errorDetails
            };
            return (TResponse)(object)errorResponse;
         }
         finally
         {
            parentSpan.SetAttribute("EndTime", invocationCompletionTime.ToLongTimeString());
         }
      }
   }
}
