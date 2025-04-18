using Azure.Messaging.ServiceBus;
using Cuata.Models;
using Cuata.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace Cuata.Services;
public class ServiceBusReceiverService : BackgroundService
{
   private readonly ServiceBusClient _client;
   private readonly ServiceBusProcessor _meetingProcessor;
   private readonly ServiceBusProcessor _presenceProcessor;
   private readonly IServiceProvider _serviceProvider;

   public ServiceBusReceiverService(IConfiguration config, IServiceProvider serviceProvider)
   {
      var connectionString = config["ServiceBusConnectionString"];
      _client = new ServiceBusClient(connectionString);

      _meetingProcessor = _client.CreateProcessor("meetings", new ServiceBusProcessorOptions
      {
         MaxConcurrentCalls = 1,
         AutoCompleteMessages = false
      });

      _presenceProcessor = _client.CreateProcessor("presence", new ServiceBusProcessorOptions
      {
         MaxConcurrentCalls = 1,
         AutoCompleteMessages = false
      });

      _meetingProcessor.ProcessMessageAsync += ProcessMeetingMessage;
      _meetingProcessor.ProcessErrorAsync += ErrorHandler;

      _presenceProcessor.ProcessMessageAsync += ProcessPresenceMessage;
      _presenceProcessor.ProcessErrorAsync += ErrorHandler;
      _serviceProvider = serviceProvider;
   }

   private async Task ProcessMeetingMessage(ProcessMessageEventArgs args)
   {
      var body = args.Message.Body.ToString();
      var meeting = JsonSerializer.Deserialize<MeetingMessage>(body);
      Console.WriteLine($"📩 Meeting Received: {meeting.Subject} @ {meeting.StartTime}");
      await args.CompleteMessageAsync(args.Message);
      await _serviceProvider.GetService<TeamsAgent>()!.RunApp(meeting.Subject);
      await Task.CompletedTask;
   }

   private async Task ProcessPresenceMessage(ProcessMessageEventArgs args)
   {
      var body = args.Message.Body.ToString();
      var presence = JsonSerializer.Deserialize<string>(body);
      Console.WriteLine($"👀 Presence Status Received: {presence}");
      await args.CompleteMessageAsync(args.Message);
      bool isPresent = presence!.Equals("present", StringComparison.OrdinalIgnoreCase);
      PresenceState.Instance.IsPresent = isPresent;
      await Task.CompletedTask;
   }

   private Task ErrorHandler(ProcessErrorEventArgs args)
   {
      Console.WriteLine($"❌ Error: {args.Exception.Message}");
      return Task.CompletedTask;
   }

   protected override async Task ExecuteAsync(CancellationToken stoppingToken)
   {
      Console.WriteLine("🟢 Starting both Service Bus receivers...");
      await _meetingProcessor.StartProcessingAsync(stoppingToken);
      await _presenceProcessor.StartProcessingAsync(stoppingToken);
      await Task.Delay(Timeout.Infinite, stoppingToken); // Keeps the service alive
   }

   public override async Task StopAsync(CancellationToken cancellationToken)
   {
      Console.WriteLine("🔴 Stopping both Service Bus receivers...");
      await _meetingProcessor.StopProcessingAsync();
      await _presenceProcessor.StopProcessingAsync();
      await _meetingProcessor.DisposeAsync();
      await _presenceProcessor.DisposeAsync();
      await base.StopAsync(cancellationToken);
   }
}
