using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Cuata.Constants;
using Cuata.Handlers;
using Cuata.Models;
using Cuata.Plugins;
using Cuata.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Cuata.Modules
{
   internal class ChatCompletionWithFunctionCalls : IModule
   {
      private readonly TracingContextCache _itemsCache;
      private readonly IKernelService _kernelService;
      private readonly Kernel _kernel;
      private readonly IOtelTracingHandler _otelTracingHandler;
      private readonly IConfiguration _configuration;
      private readonly bool _isAutomaticTelemetryEnabled;
      private readonly IServiceProvider _serviceProvider;

      public ChatCompletionWithFunctionCalls(
          TracingContextCache itemsCache,
          IKernelService kernelService,
          Kernel kernel,
          IOtelTracingHandler otelTracingHandler,
          IConfiguration configuration,
          ITelemetryToggleService telemetryToggleService,
          IServiceProvider serviceProvider)
      {
         _itemsCache = itemsCache;
         _kernelService = kernelService;
         _kernel = kernel;
         _otelTracingHandler = otelTracingHandler;
         _configuration = configuration;
         _isAutomaticTelemetryEnabled = telemetryToggleService.IsEnabled;
         _serviceProvider = serviceProvider;
      }

      public async Task RunApp()
      {
         AppContext.SetSwitch("Microsoft.SemanticKernel.Experimental.GenAI.EnableOTelDiagnosticsSensitive", _isAutomaticTelemetryEnabled);

         _itemsCache.Clear();

         var request = new RequestData
         {
            ChatHistory = new List<string>(),
            Mode = "FunctionCall"
         };

         _itemsCache.Add(OpenTelemetryConstants.GEN_AI_OPERATION_NAME_KEY, "chat");
         _itemsCache.Add(OpenTelemetryConstants.GEN_AI_REQUEST_MODEL_KEY, "gpt-4o");

         Console.ForegroundColor = ConsoleColor.Cyan;
         Console.WriteLine("🤖 AI Chat with Function Calling. Type 'quit' to exit.\n");
         Console.ResetColor();

         _kernel.ImportPluginFromObject(new TimePlugin(), "time");

         var chatHistory = new ChatHistory("You are a helpful assistant. Use available functions when needed.");

         var chatService = _kernel.GetRequiredService<IChatCompletionService>();

         while (true)
         {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("👤 You: ");
            Console.ResetColor();

            string input = Console.ReadLine()?.Trim() ?? "";


            if (string.Equals(input, "quit", StringComparison.OrdinalIgnoreCase))
            {
               Console.ForegroundColor = ConsoleColor.Red;
               Console.WriteLine("👋 Ending chat session...\n");
               Console.ResetColor();
               break;
            }

            if (string.IsNullOrWhiteSpace(input)) continue;

            chatHistory.AddUserMessage(input);
            request.ChatHistory.Add($"User: {input}");
            OpenAIPromptExecutionSettings settings = new OpenAIPromptExecutionSettings();

            settings = new()
            {
               ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
               TopP = 1,
               Temperature = 0.7
            };
            try
            {
               ChatMessageContent response;

               if (!_isAutomaticTelemetryEnabled)
               {
                  _kernel.FunctionInvocationFilters.Add(new OtelFunctionCallFilter(_serviceProvider));

                  response = await _otelTracingHandler.TraceRequest(
                      async (_) =>
                      {
                         var reply = await chatService.GetChatMessageContentAsync(chatHistory, settings, _kernel);
                         return reply;
                      },
                      request
                  );
               }
               else
               {
                  response = await chatService.GetChatMessageContentAsync(chatHistory, settings, _kernel);
               }

               if (response is ChatMessageContent chatMessage)
               {
                  chatHistory.AddAssistantMessage(chatMessage.Content!);
                  request.ChatHistory.Add($"Assistant: {chatMessage.Content}");
                  request.AssistantMessage = chatMessage.Content;

                  Console.ForegroundColor = ConsoleColor.Green;
                  Console.WriteLine($"\n🤖 AI: {chatMessage.Content}\n");
                  Console.ResetColor();
               }
               else
               {
                  Console.ForegroundColor = ConsoleColor.Red;
                  Console.WriteLine("❌ No valid response.");
                  Console.ResetColor();
               }
            }
            catch (Exception ex)
            {
               Console.ForegroundColor = ConsoleColor.Red;
               Console.WriteLine($"❌ Error: {ex.Message}");
               Console.ResetColor();
            }
         }

         Console.ForegroundColor = ConsoleColor.Magenta;
         Console.WriteLine("📜 Final Chat History:\n");
         foreach (var line in request.ChatHistory)
         {
            Console.WriteLine(line);
         }
         Console.ResetColor();
      }
   }
}
