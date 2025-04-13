using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Cuata.MeetingsIngestor.Services;

namespace Cuata.MeetingsIngestor;

public class FxMeetingNotifier
{
   private readonly CosmosDbService _cosmosService;
   private readonly ServiceBusPublisher _busPublisher;
   private readonly ILogger _logger;

   public FxMeetingNotifier(CosmosDbService cosmosService, ServiceBusPublisher busPublisher, ILogger<FxMeetingNotifier> logger)
   {
      _cosmosService = cosmosService;
      _busPublisher = busPublisher;
      _logger = logger;
   }

   [Function("MeetingNotifierFunction")]
   public async Task Run([TimerTrigger("0 */1 * * * *")] TimerInfo myTimer)
   {
      var upcomingMeetings = await _cosmosService.GetUpcomingMeetingsAsync();

      foreach (var meeting in upcomingMeetings)
      {
         await _busPublisher.SendMeetingAlertAsync(meeting);
         await _cosmosService.MarkAsNotifiedAsync(meeting);
         _logger.LogInformation($"Meeting '{meeting.subject}' at {meeting.startTime} alerted.");
      }
   }
}
