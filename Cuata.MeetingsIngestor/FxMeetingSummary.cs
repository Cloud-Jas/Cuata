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
      var boundary = GetBoundary(req.Headers);
      var reader = new MultipartReader(boundary, req.Body);
      var section = await reader.ReadNextSectionAsync();

      string meetingId = null;
      byte[] screenshotBytes = null;

      while (section != null)
      {
         var contentDisposition = section.GetContentDispositionHeader();

         if (contentDisposition.IsFileDisposition())
         {
            using var ms = new MemoryStream();
            await section.Body.CopyToAsync(ms);
            screenshotBytes = ms.ToArray();
         }
         else if (contentDisposition.IsFormDisposition())
         {
            var key = contentDisposition.Name.Value;
            var value = await new StreamReader(section.Body).ReadToEndAsync();

            if (key == "meetingId")
            {
               meetingId = value;
            }
         }

         section = await reader.ReadNextSectionAsync();
      }

      if (string.IsNullOrEmpty(meetingId) || screenshotBytes == null)
      {
         var badResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
         await badResponse.WriteStringAsync("Missing meetingId or screenshot.");
         return badResponse;
      }

      var input = new
      {
         meetingId,
         image = Convert.ToBase64String(screenshotBytes)
      };

      string instanceId = await client.ScheduleNewOrchestrationInstanceAsync("OrchestrateScreenshotSummary", input);
      var response = client.CreateCheckStatusResponse(req, instanceId);
      return response;
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
      string base64Image = input["image"];
      string meetingId = input["meetingId"];

      var imageBytes = Convert.FromBase64String(base64Image);

      var blobTask = context.CallActivityAsync<string>("SaveScreenshotToBlob", imageBytes);
      var summaryTask = context.CallActivityAsync<string>("SummarizeImageContent", imageBytes);

      await Task.WhenAll(blobTask, summaryTask);

      var blobUrl = blobTask.Result;
      var summary = summaryTask.Result;


      var summaryModel = new MeetingSummary
      {
         id = Guid.NewGuid().ToString(),
         meetingId = meetingId,
         imageUrl = blobUrl,
         summary = summary,
         timestamp = context.CurrentUtcDateTime
      };

      await context.CallActivityAsync("SaveSummaryToCosmos", summaryModel);
   }

   [Function("SaveScreenshotToBlob")]
   public async Task<string> SaveScreenshotToBlob([ActivityTrigger] byte[] imageBytes)
   {
      var conn = Environment.GetEnvironmentVariable("AzureWebJobsStorage")!;
      var blobClient = new BlobContainerClient(conn, "screenshots");
      await blobClient.CreateIfNotExistsAsync();

      var blobName = $"screenshot-{DateTime.UtcNow:yyyyMMddHHmmssfff}.png";
      var blob = blobClient.GetBlobClient(blobName);
      await blob.UploadAsync(new MemoryStream(imageBytes), overwrite: true);

      return blob.Uri.ToString();
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

   [Function("OrchestrateConsolidatedSummary")]
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

      history.AddUserMessage("You are a meeting analyst assistant. Analyze the following summaries from different meeting screenshots.");
      history.AddUserMessage("Step 1: Identify which summaries contain very important insights or action points.");
      history.AddUserMessage("Step 2: Provide a consolidated overall summary of the meeting.");
      history.AddUserMessage("Step 3: Return only the image URLs that correspond to those important summaries. Output format should be JSON like this:\n" +
                             "{ \"FinalSummary\": \"...\", \"ImportantImageUrls\": [\"https://...\", \"https://...\"] }");

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
