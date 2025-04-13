using Azure.Messaging.ServiceBus;
using Cuata.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace Cuata.Services;
public class ServiceBusReceiverService : BackgroundService
{
   private readonly ServiceBusProcessor _processor;

   public ServiceBusReceiverService(IConfiguration config)
   {
      var connectionString = config["ServiceBusConnectionString"];
      var queueName = config["ServiceBusQueueName"];
      var client = new ServiceBusClient(connectionString);

      _processor = client.CreateProcessor(queueName, new ServiceBusProcessorOptions
      {
         MaxConcurrentCalls = 1,
         AutoCompleteMessages = false
      });

      _processor.ProcessMessageAsync += ProcessMessageHandler;
      _processor.ProcessErrorAsync += ErrorHandler;
   }

   private async Task ProcessMessageHandler(ProcessMessageEventArgs args)
   {
      var body = args.Message.Body.ToString();
      var meeting = JsonSerializer.Deserialize<MeetingMessage>(body);

      Console.WriteLine($"📩 Message Received: {meeting.Subject} @ {meeting.StartTime}");

      await args.CompleteMessageAsync(args.Message);
   }

   private Task ErrorHandler(ProcessErrorEventArgs args)
   {
      Console.WriteLine($"❌ Error: {args.Exception.Message}");
      return Task.CompletedTask;
   }

   protected override async Task ExecuteAsync(CancellationToken stoppingToken)
   {
      await _processor.StartProcessingAsync(stoppingToken);
      Console.WriteLine("🟢 Service Bus receiver started");
      await Task.Delay(Timeout.Infinite, stoppingToken);
   }

   public override async Task StopAsync(CancellationToken cancellationToken)
   {
      await _processor.StopProcessingAsync();
      await _processor.DisposeAsync();
      Console.WriteLine("🔴 Service Bus receiver stopped");
      await base.StopAsync(cancellationToken);
   }
}
