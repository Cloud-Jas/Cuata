using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel;
using Cuata.Handlers;
using Cuata.Modules;
using OpenAI.Assistants;
using OpenAI.Chat;
using Newtonsoft.Json;
using OpenTelemetry.Trace;
using Cuata.Models;
using Microsoft.Extensions.DependencyInjection;
using Cuata.Services;
using Cuata.Constants;
using System.Diagnostics;
using Azure.AI.OpenAI.Chat;
#pragma warning disable SKEXP0110
#pragma warning disable SKEXP0001

public class MultiAgentInteractions : IModule
{
   private const string ReviewerName = "ArtDirector";
   private const string CopyWriterName = "CopyWriter";
   private readonly IChatCompletionService chatCompletionService;
   private readonly Kernel kernel;
   private readonly TracingContextCache _itemsCache;
   private readonly Tracer _tracer;
   private readonly bool _isAutomaticTelemetryEnabled;
   public MultiAgentInteractions(IChatCompletionService chatService, Kernel kernel, 
      TracerProvider tracerProvider,ITelemetryToggleService telemetryToggleService,
      TracingContextCache itemsCache)
   {
      chatCompletionService = chatService;
      this.kernel = kernel;
      _itemsCache = itemsCache;
      _tracer = tracerProvider.GetTracer("OtelDemo");
      _isAutomaticTelemetryEnabled = telemetryToggleService.IsEnabled;
   }
   public async Task RunApp()
   {
      AppContext.SetSwitch("Microsoft.SemanticKernel.Experimental.GenAI.EnableOTelDiagnosticsSensitive", _isAutomaticTelemetryEnabled);

      _itemsCache.Clear();

      _itemsCache.Add(OpenTelemetryConstants.GEN_AI_OPERATION_NAME_KEY, "create_agent");
      _itemsCache.Add(OpenTelemetryConstants.GEN_AI_REQUEST_MODEL_KEY, "gpt-4o");

      var reviewer = new ChatCompletionAgent
      {
         Name = ReviewerName,
         Instructions = """
                You are an art director who has opinions about copywriting born of a love for David Ogilvy.
            The goal is to determine if the given copy is acceptable to print.
            If so, state that it is approved.
            If not, provide insight on how to refine suggested copy without examples.
            """,
         Kernel = kernel
      };

      var writer = new ChatCompletionAgent
      {
         Name = CopyWriterName,
         Instructions = """
                You are a copywriter with ten years of experience and are known for brevity and a dry humor.
            The goal is to refine and decide on the single best copy as an expert in the field.
            Only provide a single proposal per response.
            Never delimit the response with quotation marks.
            You're laser focused on the goal at hand.
            Don't waste time with chit chat.
            Consider suggestions when refining an idea.
            """,
         Kernel = kernel
      };

      var terminationFn = AgentGroupChat.CreatePromptFunctionForStrategy(
           """
                Determine if the copy has been approved.  If so, respond with a single word: yes

                History:
                {{$history}}
                """,
          safeParameterNames: "history");

      var reducer = new ChatHistoryTruncationReducer(1);
      
      KernelFunction selectionFunction = KernelFunctionFactory.CreateFromPrompt(
                $$$"""
                Your job is to determine which participant takes the next turn in a conversation according to the action of the most recent participant.
                State only the name of the participant to take the next turn.
                
                Choose only from these participants:
                - {{{ReviewerName}}}
                - {{{CopyWriterName}}}
                
                Always follow these rules when selecting the next participant:
                - After user input, it is {{{CopyWriterName}}}'a turn.
                - After {{{CopyWriterName}}} replies, it is {{{ReviewerName}}}'s turn.
                - After {{{ReviewerName}}} provides feedback, it is {{{CopyWriterName}}}'s turn.
                History:
                {{$history}}
                """);
      var chat = new AgentGroupChat(writer, reviewer)
      {
         ExecutionSettings = new AgentGroupChatSettings
         {
            SelectionStrategy = new KernelFunctionSelectionStrategy(selectionFunction, kernel)
            {
               InitialAgent = writer,
               ResultParser = r => r.GetValue<string>() ?? CopyWriterName,
               HistoryVariableName = "history",
               HistoryReducer = reducer,
               EvaluateNameOnly = true
            },
            TerminationStrategy = new KernelFunctionTerminationStrategy(terminationFn, kernel)
            {
               Agents = [reviewer],
               ResultParser = r => r.GetValue<string>()?.Contains("yes", StringComparison.OrdinalIgnoreCase) ?? false,
               HistoryVariableName = "history",
               HistoryReducer = reducer,
               MaximumIterations = 10
            }
         }
      };

      var userMessage = new Microsoft.SemanticKernel.ChatMessageContent(AuthorRole.User, "concept: maps made out of egg cartons.");
      chat.AddChatMessage(userMessage);

      var chatHistory = new ChatHistory();
      chatHistory.AddUserMessage(userMessage.Content!);

      if (!_isAutomaticTelemetryEnabled)
      {
         var chatEnumerator = chat.InvokeAsync().GetAsyncEnumerator();

         try
         {
            using var parentSpan = _tracer.StartActiveSpan($"{_itemsCache[OpenTelemetryConstants.GEN_AI_OPERATION_NAME_KEY]}{_itemsCache[OpenTelemetryConstants.GEN_AI_REQUEST_MODEL_KEY]}", SpanKind.Client);

            parentSpan.SetAttribute("StartTime", DateTime.UtcNow.ToLongTimeString());

            while (await chatEnumerator.MoveNextAsync())
            {

               using var span = _tracer.StartActiveSpan($"{_itemsCache[OpenTelemetryConstants.GEN_AI_OPERATION_NAME_KEY]}{_itemsCache[OpenTelemetryConstants.GEN_AI_REQUEST_MODEL_KEY]}", SpanKind.Client);

               var message = chatEnumerator.Current;
               
               span.SetAttribute(OpenTelemetryConstants.GEN_AI_AGENT_NAME, message.AuthorName);
               span.SetAttribute(OpenTelemetryConstants.GEN_AI_AGENT_ID, (message.InnerContent as OpenAI.Chat.ChatCompletion)!.Id);
               span.SetAttribute(OpenTelemetryConstants.GEN_AI_USAGE_INPUT_TOKENS_KEY, (message.InnerContent as OpenAI.Chat.ChatCompletion)!.Usage.InputTokenCount);
               span.SetAttribute(OpenTelemetryConstants.GEN_AI_USAGE_OUTPUT_TOKENS_KEY, (message.InnerContent as OpenAI.Chat.ChatCompletion)!.Usage.OutputTokenCount);
               span.SetAttribute(OpenTelemetryConstants.GEN_AI_REQUEST_MAX_TOKENS_KEY, (message.InnerContent as OpenAI.Chat.ChatCompletion)!.Usage.TotalTokenCount);
               span.SetAttribute(OpenTelemetryConstants.GEN_AI_RESPONSE_FINISH_REASONS_KEY, (message.InnerContent as OpenAI.Chat.ChatCompletion)!.FinishReason.ToString());
               span.SetAttribute(OpenTelemetryConstants.GEN_AI_RESPONSE_ID_KEY, (message.InnerContent as OpenAI.Chat.ChatCompletion)!.Id.ToString());
               span.SetAttribute(OpenTelemetryConstants.GEN_AI_RESPONSE_MODEL_KEY, (message.InnerContent as OpenAI.Chat.ChatCompletion)!.Model.ToString());

               WriteAgentChatMessage(message);

               span.SetAttribute("EndTime", DateTime.UtcNow.ToLongTimeString());
            }

            parentSpan.SetAttribute("EndTime", DateTime.UtcNow.ToLongTimeString());

         }
         catch (Exception ex)
         {
            Console.WriteLine($"Error: {ex.Message}");
         }
         finally
         {
            await chatEnumerator.DisposeAsync();
         }
      }

      else
      {
         await foreach (var message in chat.InvokeAsync())
         {
            WriteAgentChatMessage(message);
         }
      }

      ConsoleColor originalColor = Console.ForegroundColor;
      Console.ForegroundColor = chat.IsComplete ? ConsoleColor.Green : ConsoleColor.Red;

      string statusEmoji = chat.IsComplete ? "✅" : "🕒";
      string statusText = chat.IsComplete ? "Chat Completed" : "Chat Incomplete";

      Console.WriteLine($"\n{statusEmoji} [{statusText}]");

      Console.ForegroundColor = originalColor;
   }

   void WriteAgentChatMessage(Microsoft.SemanticKernel.ChatMessageContent message)
   {
      ConsoleColor originalColor = Console.ForegroundColor;

      string author = message.AuthorName ?? "Agent";
      string emoji = "🤖";
      ConsoleColor messageColor = ConsoleColor.Gray;

      // Determine message color and emoji based on the agent name
      if (author == "CopyWriter")
      {
         emoji = "✍️";
         messageColor = ConsoleColor.Yellow;
      }
      else if (author == "ArtDirector")
      {
         emoji = "🎨";
         messageColor = ConsoleColor.Magenta;
      }
      else if (message.Role == AuthorRole.User)
      {
         emoji = "👤";
         messageColor = ConsoleColor.Cyan;
         author = "You";
      }
      else if (message.Role == AuthorRole.System)
      {
         emoji = "🛠️";
         messageColor = ConsoleColor.DarkCyan;
         author = "System";
      }

      // Output the message with style
      Console.ForegroundColor = messageColor;
      Console.WriteLine($"\n{emoji} [{author}]: {message.Content}");

      // Reset to a neutral color for content extras
      Console.ForegroundColor = ConsoleColor.DarkGray;

      foreach (KernelContent item in message.Items)
      {
         switch (item)
         {
            case AnnotationContent annotation:
               Console.WriteLine($"  📝 [Annotation] \"{annotation.Quote}\" - 📄 File #{annotation.FileId}");
               break;
            case FileReferenceContent fileReference:
               Console.WriteLine($"  📎 [File Reference] File #{fileReference.FileId}");
               break;
            case FunctionCallContent functionCall:
               Console.WriteLine($"  📞 [Function Call] {functionCall.Id}");
               break;
            case FunctionResultContent functionResult:
               Console.WriteLine($"  ✅ [Function Result] {functionResult.CallId} - {functionResult.Result ?? "*"}");
               break;
         }
      }

      Console.ForegroundColor = originalColor;

}
}
