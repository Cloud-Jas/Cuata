using Azure.Messaging.ServiceBus;
using Cuata.MeetingsIngestor.Models;
using System.Text.Json;

namespace Cuata.MeetingsIngestor.Services;

public class ServiceBusPublisher
{
   private ServiceBusSender _sender;
   private readonly ServiceBusClient serviceBusClient;
   public ServiceBusPublisher(ServiceBusClient client)
   {
      serviceBusClient = client;
   }

   public async Task SendMeetingAlertAsync(Meeting meeting)
   {
      _sender = serviceBusClient.CreateSender("meetings");
      var msg = new ServiceBusMessage(JsonSerializer.Serialize(meeting));
      await _sender.SendMessageAsync(msg);
   }
   public async Task SendConsolidatedSummaryAsync(ConsolidatedSummaryResult summary)
   {
      _sender = serviceBusClient.CreateSender("meetingsconsolidated");
      var messageBody = new ServiceBusMessage(JsonSerializer.Serialize(summary));
      await _sender.SendMessageAsync(messageBody);
   }
}
