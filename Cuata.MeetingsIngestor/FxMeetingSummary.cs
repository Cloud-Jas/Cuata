using System.Text.Json;
using System.Net;
using Azure.Storage.Blobs;
using Cuata.MeetingsIngestor.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask;
using Cuata.MeetingsIngestor.Services;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI.Chat;
using System.Net.Mail;

namespace Cuata.MeetingsIngestor.Functions;

public class FxMeetingSummary
{
   private readonly ILogger _logger;
   private readonly Kernel _kernel;

   public FxMeetingSummary(ILogger<FxMeetingSummary> logger, Kernel kernel)
   {
      _kernel = kernel;
      _logger = logger;
   }
   private string GetBoundary(HttpHeadersCollection headers)
   {
      var contentType = headers.GetValues("Content-Type").FirstOrDefault();
      var elements = contentType?.Split(';');
      var boundary = elements?.FirstOrDefault(e => e.Trim().StartsWith("boundary=", StringComparison.OrdinalIgnoreCase));
      return boundary?.Substring("boundary=".Length).Trim('"');
   }

   [Function("HttpStartScreenshotSummary")]
   public async Task<HttpResponseData> HttpStartAsync(
       [HttpTrigger(AuthorizationLevel.Function, "post", Route = "screenshotSummary")] HttpRequestData req,
       [DurableClient] DurableTaskClient client)
   {
      try
      {
         var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
         var data = JsonSerializer.Deserialize<ScreenshotRequest>(requestBody, new JsonSerializerOptions
         {
            PropertyNameCaseInsensitive = true
         });

         if (string.IsNullOrWhiteSpace(data?.MeetingId) || string.IsNullOrWhiteSpace(data?.BlobUri))
         {
            var badResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await badResponse.WriteStringAsync("Missing meetingId or blobUri.");
            return badResponse;
         }

         var input = new
         {
            meetingId = data.MeetingId,
            blobUri = data.BlobUri
         };

         string instanceId = await client.ScheduleNewOrchestrationInstanceAsync("OrchestrateScreenshotSummary", input);
         return client.CreateCheckStatusResponse(req, instanceId);
      }
      catch (Exception ex)
      {
         var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
         await errorResponse.WriteStringAsync($"Error processing the request: {ex.Message}");
         return errorResponse;
      }
   }

   [Function("HttpStartConsolidatedSummary")]
   public async Task<HttpResponseData> HttpStartConsolidatedSummaryAsync(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = "consolidateSummary")] HttpRequestData req,
    [DurableClient] DurableTaskClient client)
   {
      string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
      var input = JsonSerializer.Deserialize<Dictionary<string, string>>(requestBody);

      string meetingId = input?.GetValueOrDefault("meetingId");
      if (string.IsNullOrWhiteSpace(meetingId))
      {
         var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
         await badResponse.WriteStringAsync("Missing meetingId in request body.");
         return badResponse;
      }

      string instanceId = await client.ScheduleNewOrchestrationInstanceAsync("OrchestrateGenerateAndPublishSummary", meetingId);
      return client.CreateCheckStatusResponse(req, instanceId);
   }


   [Function("OrchestrateScreenshotSummary")]
   public async Task RunOrchestratorAsync(
       [OrchestrationTrigger] TaskOrchestrationContext context)
   {
      var input = context.GetInput<Dictionary<string, string>>();
      string blobUri = input["blobUri"];
      string meetingId = input["meetingId"];

      var summary= await context.CallActivityAsync<string>("SummarizeImageContent", blobUri);

      var summaryModel = new MeetingSummary
      {
         id = Guid.NewGuid().ToString(),
         meetingId = meetingId,
         imageUrl = blobUri,
         summary = summary,
         timestamp = context.CurrentUtcDateTime
      };

      await context.CallActivityAsync("SaveSummaryToCosmos", summaryModel);
   }
   [Function("SummarizeImageContent")]
   public async Task<string> SummarizeImageContent([ActivityTrigger] string blobUrl)
   {
      try
      {
         var items = new ChatMessageContentItemCollection
    {
        new TextContent($"You are a meeting assistant. Summarize the content of this meeting screenshot: Make sure you capture all the important details and provide a summary."),
        new ImageContent(new Uri(blobUrl))
    };

         var history = new ChatHistory();
         history.AddUserMessage(items);

         var chatService = _kernel.GetRequiredService<IChatCompletionService>();

         var chatMessage = await chatService.GetChatMessageContentsAsync(history);

         var result = chatMessage.FirstOrDefault()?.Content?.Trim() ?? "No response from model.";

         return result.ToString();
      }
      catch (Exception ex)
      {
         _logger.LogError(ex, "Error summarizing image content");
         return "Error summarizing image content";
      }
   }

   [Function("SaveSummaryToCosmos")]
   public async Task SaveSummaryToCosmos(
    [ActivityTrigger] MeetingSummary summary,
    FunctionContext executionContext)
   {
      var service = executionContext.InstanceServices.GetRequiredService<CosmosMeetingSummaryService>();
      await service.UpsertSummaryAsync(summary);
   }

   [Function("OrchestrateGenerateAndPublishSummary")]
   public async Task RunConsolidatedSummaryOrchestratorAsync([OrchestrationTrigger] TaskOrchestrationContext context)
   {
      var meetingId = context.GetInput<string>();
      var summaries = await context.CallActivityAsync<List<MeetingSummary>>("GetMeetingSummariesFromCosmos", meetingId);
      var consolidated = await context.CallActivityAsync<ConsolidatedSummaryResult>("GenerateConsolidatedSummary", summaries);
      await context.CallActivityAsync("PublishSummaryToServiceBus", consolidated);
   }

   [Function("GetMeetingSummariesFromCosmos")]
   public async Task<List<MeetingSummary>> GetMeetingSummariesFromCosmos([ActivityTrigger] string meetingId,
    FunctionContext context)
   {
      var service = context.InstanceServices.GetRequiredService<CosmosMeetingSummaryService>();
      return await service.GetSummariesForMeetingAsync(meetingId);
   }

   [Function("GenerateConsolidatedSummary")]
   public async Task<ConsolidatedSummaryResult> GenerateConsolidatedSummary([ActivityTrigger] List<MeetingSummary> summaries)
   {
      var chatService = _kernel.GetRequiredService<IChatCompletionService>();
      var history = new ChatHistory();

      history.AddSystemMessage("""
         You are a meeting analyst assistant. Analyze the following summaries from different meeting screenshots.
         You are provided with a list of summaries and their corresponding image URLs.
         Identify which summaries contain very important insights or action points.
         Try to consolidate the summaries into a single overall summary of the meeting.
         Make sure to capture all the important details and provide a summary for 100 lines.
         Don't miss any important details.
         
         Return in the below format don't add ```json and ``` at the start and end of the response.
         
         {
            "FinalSummary": "...",
            "ImportantImageUrls": ["https://...", "https://..."]
         }

         Example:
         {
            "FinalSummary": "The meeting discussed the project timeline and deliverables. Action items include...",
            "ImportantImageUrls": ["https://example.com/image1.jpg", "https://example.com/image2.jpg"]
         }

         """);

      foreach (var s in summaries)
      {
         history.AddUserMessage($"Summary from image: {s.imageUrl}\n\n{s.summary}");
      }

      var result = await chatService.GetChatMessageContentsAsync(history);
      var content = result.FirstOrDefault()?.Content;

      if (string.IsNullOrWhiteSpace(content))
      {
         return new ConsolidatedSummaryResult
         {
            FinalSummary = "Unable to generate summary.",
            ImportantImageUrls = new List<string>()
         };
      }

      try
      {
         return JsonSerializer.Deserialize<ConsolidatedSummaryResult>(content, new JsonSerializerOptions
         {
            PropertyNameCaseInsensitive = true
         }) ?? new ConsolidatedSummaryResult();
      }
      catch (Exception ex)
      {
         _logger.LogError(ex, "Failed to parse LLM response: {Response}", content);
         return new ConsolidatedSummaryResult
         {
            FinalSummary = content,
            ImportantImageUrls = new List<string>()
         };
      }
   }

   [Function("PublishSummaryToServiceBus")]
   public async Task PublishSummaryToServiceBus(
    [ActivityTrigger] ConsolidatedSummaryResult summary,
    FunctionContext context)
   {
      var publisher = context.InstanceServices.GetRequiredService<ServiceBusPublisher>();
      await publisher.SendConsolidatedSummaryAsync(summary);
   }
}
