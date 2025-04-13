using Azure.Messaging.ServiceBus;
using Cuata.MeetingsIngestor.Models;
using System.Text.Json;

namespace Cuata.MeetingsIngestor.Services;

public class ServiceBusPublisher
{
   private readonly ServiceBusSender _sender;

   public ServiceBusPublisher(ServiceBusClient client, string topicName)
   {
      _sender = client.CreateSender(topicName);
   }

   public async Task SendMeetingAlertAsync(Meeting meeting)
   {
      var msg = new ServiceBusMessage(JsonSerializer.Serialize(meeting));
      await _sender.SendMessageAsync(msg);
   }
}
