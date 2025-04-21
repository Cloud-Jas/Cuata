using Cuata.Constants;
using Cuata.Handlers;
using Cuata.Models;
using Cuata.Plugins;
using Cuata.Services;
using Microsoft.CognitiveServices.Speech;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Cuata.Modules
{
   public class TeamsAgent : IModule
   {
      private readonly TracingContextCache _itemsCache;
      private readonly IKernelService _kernelService;
      private readonly Kernel _kernel;
      private readonly IOtelTracingHandler _otelTracingHandler;
      private readonly IConfiguration _configuration;
      private readonly bool _isAutomaticTelemetryEnabled;
      private readonly IServiceProvider _serviceProvider;
      private readonly SpeechRecognizer _recognizer;

      public TeamsAgent(
          TracingContextCache itemsCache,
          IKernelService kernelService,
          Kernel kernel,
          IOtelTracingHandler otelTracingHandler,
          IConfiguration configuration,
          ITelemetryToggleService telemetryToggleService,
          IServiceProvider serviceProvider,
          SpeechRecognizer speechRecognizer)
      {
         _itemsCache = itemsCache;
         _kernelService = kernelService;
         _kernel = kernel;
         _otelTracingHandler = otelTracingHandler;
         _configuration = configuration;
         _isAutomaticTelemetryEnabled = telemetryToggleService.IsEnabled;
         _serviceProvider = serviceProvider;
         _recognizer = speechRecognizer;
      }


      public async Task RunApp(string? meetingTitle)
      {
         if (string.IsNullOrWhiteSpace(meetingTitle))
            meetingTitle = "Cuata demo";

         bool userPresent = CuataState.Instance.IsPresent;

         CuataState.Instance.OnPresenceChanged += isPresent =>
         {
            Console.WriteLine("Presence callback: " + isPresent);
         };

         AppContext.SetSwitch("Microsoft.SemanticKernel.Experimental.GenAI.EnableOTelDiagnosticsSensitive", _isAutomaticTelemetryEnabled);

         _itemsCache.Clear();

         var request = new RequestData
         {
            ChatHistory = new List<string>(),
            Mode = "FunctionCall"
         };

         if (_kernel.Plugins.Count > 0)
         {
            _kernel.Plugins.Clear();
         }

         _itemsCache.Add(OpenTelemetryConstants.GEN_AI_OPERATION_NAME_KEY, "chat");
         _itemsCache.Add(OpenTelemetryConstants.GEN_AI_REQUEST_MODEL_KEY, "gpt-4o");

         Console.ForegroundColor = ConsoleColor.Cyan;

         _kernel.ImportPluginFromObject(new TeamsPlugin(), "TeamsPlugin");
         _kernel.ImportPluginFromObject(new TimePlugin(), "TimePlugin");
         _kernel.ImportPluginFromObject(new MousePlugin(), "MousePlugin");
         _kernel.ImportPluginFromObject(new ChromePlugin(), "ChromePlugin");
         _kernel.ImportPluginFromObject(new ScreenshotPlugin(_kernel), "ScreenshotPlugin");
         _kernel.ImportPluginFromObject(new LocatePlugin(_kernel, _serviceProvider), "LocatePlugin");
         _kernel.ImportPluginFromObject(new SummarizePlugin(_kernel), "SummarizePlugin");
         _kernel.ImportPluginFromObject(new WordPlugin(), "WordPlugin");
         _kernel.ImportPluginFromObject(new KeyboardPlugin(), "KeyboardPlugin");

         var chatHistory = new ChatHistory();

         chatHistory.AddSystemMessage(
            """
            You are a helpful assistant that can help with Microsoft Teams related tasks.
            You can help listening to the meetings while the user is away.

            Goal: Your goal is to grasp the context of the conversation and provide relevant information to the user when 
            they are back.

            You have access to below Plugins:

            Time Actions — `TimePlugin`:

            - `GetCurrentTime`: Returns the current time in the format HH:mm:ss.
            - `GetCurrentDate`: Returns the current date in the format YYYY-MM-DD.
            
            🖼️ Locate In Screenshot Actions — `LocatePlugin`:
            
            - `LocateElementInScreenshot("search text")`: Locates the specified text in the screenshot and returns the coordinates of the UI element in the format X,Y.
            
            Teams Actions — `TeamsPlugin`:

            - `OpenTeams`: Opens Microsoft Teams.

            📸 Screenshot Actions — `ScreenshotPlugin`:
            
            - `TakeScreenshotAndVerify`: Takes a screenshot and verifies if the action was successful.
            
            🖱️ Mouse Actions — `MousePlugin`:
            
            - `MoveMouse(x, y)`: Moves the mouse to the specified screen coordinates.
            - `LeftClick()`: Performs a left mouse click.
            - `RightClick()`: Performs a right-click.
            - `Scroll(amount)`: Scrolls the screen by a specified amount (positive or negative).
            
            ---
            
            ⌨️ Keyboard Actions — `KeyboardPlugin`:
            
            - `TypeText("some text")`: Types text into the currently focused input field.
            - `PressKey("Enter")`: Presses the Enter key.
            - `PressKey("Tab")`: Navigates to the next field.
            - `PressKey("Ctrl+C")`, `PressKey("Ctrl+V")`: Supports common shortcuts.
            - Use `KeyboardShortcut("Ctrl+Shift+T")` for complex combinations.


            Strategy:

            1. Open Microsoft Teams.
            2. Once Microsoft teams is open, you need to search for "calendar" and click on it.
            3. Take a screenshot of the screen and verify if the calendar is open.
            4. Once you open the calendar, you need to search for the meeting with the title that you received in the context that happens today and click on it.
            5. Take a screenshot of the screen and verify if the small popup opens with Join button in it.
            6. Once the popup opens, you need to search for "join" button and click on it.
            7. Take a screenshot of the screen and verify if the meeting window opens and "Join now" button is present in it.
            8. In the meeting popup window you need to search for "Join now" button and click on it.
            9. Take a screenshot of the screen and verify if you are inside the meeting.
            10. Maximize the meeting window by pressing "Win" + "Up" key.
            11. Once you are inside the meeting, you need to search for "Mic" exact word button and click on it.
            12. Take a screenshot of the screen and verify if you are muted.
            13. Once you are muted, search for "more" button and click on it.
            14. Take a screenshot of the screen and verify if the more options menu is open.
            15. Once the more options menu is open, search for "Record and transcribe" button and don't click on it.
            16. Take a screenshot of the screen and verify if the popup with "Start transcription" button is open.
            17. Once the popup is open, search for "Start transcription" button and click on it.            
            18. If it asks for permission, click on "Confirm" button"
            19. Take a screenshot of the screen and verify if the transcription is started.
            20. Start listening to the meeting and take notes.
            21. Once the meeting is over, search for "Leave" button and click on it.
            22. Take a screenshot of the screen and verify if you are out of the meeting.
            23. Once you are out of the meeting, share the notes with the user.

            """);

         chatHistory.AddUserMessage($"User present status: {userPresent} and the meeting: {meetingTitle} is about to happen");
         var chatService = _kernel.GetRequiredService<IChatCompletionService>();

         OpenAIPromptExecutionSettings settings = new OpenAIPromptExecutionSettings();

         settings = new()
         {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            TopP = 1,
            Temperature = 0.2
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
   }
}
