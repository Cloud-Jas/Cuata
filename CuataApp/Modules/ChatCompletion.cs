using Cuata.Models;
using Cuata.Services;
using Microsoft.SemanticKernel;
using System.Text;
using Cuata.Handlers;
using Cuata.Constants;
using Microsoft.Extensions.Configuration;

namespace Cuata.Modules
{
   public class ChatCompletion : IModule
   {
      private readonly TracingContextCache _itemsCache;
      private readonly IKernelService _kernelService;
      private readonly Kernel _kernel;
      private readonly IOtelTracingHandler _otelTracingHandler;
      private readonly IConfiguration _configuration;
      private readonly bool _isAutomaticTelemetryEnabled;

      public ChatCompletion(TracingContextCache itemsCache,
                            IKernelService kernelService,
                            Kernel kernel,
                            IOtelTracingHandler otelTracingHandler,
                            IConfiguration configuration,
                            ITelemetryToggleService telemetryToggleService)
      {
         _kernelService = kernelService;
         _itemsCache = itemsCache;
         _kernel = kernel;
         _otelTracingHandler = otelTracingHandler;
         _configuration = configuration;
         _isAutomaticTelemetryEnabled = telemetryToggleService.IsEnabled;
      }

      public async Task RunApp()
      {

         AppContext.SetSwitch("Microsoft.SemanticKernel.Experimental.GenAI.EnableOTelDiagnosticsSensitive", _isAutomaticTelemetryEnabled);

         _itemsCache.Clear();

         var request = new RequestData
         {
            ChatHistory = new List<string>(),
            Mode = "Chat"
         };

         Console.ForegroundColor = ConsoleColor.Cyan;
         Console.WriteLine("💬 Starting Chat with AI. Type 'quit' to exit.\n");
         Console.ResetColor();

         while (true)
         {
            _itemsCache[OpenTelemetryConstants.GEN_AI_OPERATION_NAME_KEY] = "chat";
            _itemsCache[OpenTelemetryConstants.GEN_AI_REQUEST_MODEL_KEY] = "gpt-4o";

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

            request.UserQuery = input;
            request.ChatHistory.Add($"User: {input}");

            try
            {
               ChatMessageContent response;
               if (!_isAutomaticTelemetryEnabled)
               {
                  response = await _otelTracingHandler.TraceRequest(
                     async (req) =>
                     {
                        string prompt = BuildPromptWithHistory(req.ChatHistory!);
                        return await _kernelService.GetChatMessageContentAsync(_kernel, prompt);
                     },
                     request
                  );
               }
               else
               {
                  response = await _kernelService.GetChatMessageContentAsync(_kernel, BuildPromptWithHistory(request.ChatHistory));
               }

               if (response is ChatMessageContent chatMessage)
               {
                  request.ChatHistory.Add($"Assistant: {chatMessage.Content}");
                  request.AssistantMessage = chatMessage.Content;
                  Console.ForegroundColor = ConsoleColor.Green;
                  Console.WriteLine($"\n🤖 AI: {chatMessage.Content}\n");
                  Console.ResetColor();
               }
               else
               {
                  Console.ForegroundColor = ConsoleColor.Red;
                  Console.WriteLine("❌ Something went wrong. Please try again.");
                  Console.ResetColor();
               }
            }
            catch (Exception ex)
            {
               Console.ForegroundColor = ConsoleColor.Red;
               Console.WriteLine($"❌ Something went wrong. Please try again.");
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

      private string BuildPromptWithHistory(List<string> chatHistory)
      {
         var promptBuilder = new StringBuilder();
         
         promptBuilder.AppendLine("You are a helpful assistant having a conversation with a user.");

         foreach (var entry in chatHistory)
         {
            promptBuilder.AppendLine(entry);
         }

         promptBuilder.Append("Assistant:");
         return promptBuilder.ToString();
      }
   }
}
