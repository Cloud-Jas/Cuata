using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace Cuata.OpenCv.Services;
public class ServiceBusPublisherService
{
   private readonly ServiceBusProcessor _processor;
   private readonly ServiceBusClient client;
   public ServiceBusPublisherService(IConfiguration config)
   {
      var connectionString = config["ServiceBusConnectionString"];
      client = new ServiceBusClient(connectionString);
   }
   public async Task SendMessageAsync(string message)
   {
      try
      {
         var sender = client.CreateSender("presence");
         var serviceBusMessage = new ServiceBusMessage(JsonSerializer.Serialize(message));
         await sender.SendMessageAsync(serviceBusMessage);
      }
      catch (Exception ex)
      {
         Console.WriteLine($"❌ Error: {ex.Message}");
      }
   }
}
