using Cuata.MeetingsIngestor.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Cuata.MeetingsIngestor
{
   public class FxMeetingIngestor
   {
      private readonly GraphService _graphService;
      private readonly CosmosDbService _cosmosService;
      private readonly ILogger<FxMeetingIngestor> _logger;

      public FxMeetingIngestor(GraphService graphService, CosmosDbService cosmosService, ILogger<FxMeetingIngestor> logger)
      {
         _graphService = graphService;
         _cosmosService = cosmosService;
         _logger = logger;
      }

      [Function("MeetingSyncFunction")]
      public async Task RunAsync([TimerTrigger("0 */3 * * * *")] TimerInfo timer, FunctionContext context)
      {
         var logger = context.GetLogger("FxMeetingIngestor");
         logger.LogInformation("FxMeetingIngestor triggered at: {time}", DateTime.UtcNow);

         var userId = "administrator@iamdivakarkumar.com";

         var todayMeetings = await _graphService.GetTodayMeetingsAsync(userId);
         var existingMeetings = await _cosmosService.GetMeetingsForTodayAsync(userId);

         if(todayMeetings == null || todayMeetings.Count == 0)
         {
            logger.LogInformation("No meetings found for today.");
            return;
         }
         if (existingMeetings != null && existingMeetings.Count != 0)
         {

            var deletedMeetings = existingMeetings
                .Where(existing => !todayMeetings.Any(m => m.id == existing.id))
                .ToList();

            foreach (var deleted in deletedMeetings)
            {
               logger.LogInformation($"Soft deleting cancelled meeting: {deleted.subject}");
               deleted.cancelled = true;
               await _cosmosService.UpsertMeetingAsync(deleted);
            }
         }
         foreach (var meeting in todayMeetings)
         {
            await _cosmosService.UpsertMeetingAsync(meeting);
            logger.LogInformation($"Upserted meeting: {meeting.subject}");
         }
      }

   }
}
