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
using System.Text.Json;
using Azure.Core;

namespace Cuata.Modules
{
   internal class CuaAgent : IModule
   {
      private readonly TracingContextCache _itemsCache;
      private readonly IKernelService _kernelService;
      private readonly Kernel _kernel;
      private readonly IOtelTracingHandler _otelTracingHandler;
      private readonly IConfiguration _configuration;
      private readonly bool _isAutomaticTelemetryEnabled;
      private readonly IServiceProvider _serviceProvider;

      public CuaAgent(
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

      public async Task ExecutePlannedActionsAsync(string userGoal)
      {

         var actions = await GetActionStepsAsync(userGoal);

         _kernel.ImportPluginFromObject(new TimePlugin(), "TimePlugin");
         _kernel.ImportPluginFromObject(new KeyboardPlugin(), "KeyboardPlugin");
         _kernel.ImportPluginFromObject(new MousePlugin(), "MousePlugin");
         _kernel.ImportPluginFromObject(new ChromePlugin(), "ChromePlugin");
         _kernel.ImportPluginFromObject(new ScreenshotPlugin(_kernel), "ScreenshotPlugin");

         for (int i = 0; i < actions.Count; i++)
         {
            string currentStep = actions[i];
            var request = new RequestData
            {
               ChatHistory = new List<string>(),
               Mode = "FunctionCall"
            };

            var chatHistory = new ChatHistory();

            chatHistory.AddSystemMessage(
               """
            You are a powerful desktop automation assistant capable of observing the screen visually, reasoning about UI elements, and performing system-level actions on behalf of the user.

            You work like a human: you see the screen, think, and interact using the mouse and keyboard. You must always reason based on the screenshot and available context.

            ---

            📸 Visual Understanding:

            - Always start with a request to screenshot of the current screen using the `ScreenshotPlugin`.
            - Based on the screenshot, you can analyze the screen visually and come up with tasks to perform based on the user’s request.
            - If you are already on the right screen, you can continue with the same screen and perform the action
            - If not on the right screen, you can use plugins to navigate to the right screen.
            - You can use Scroll and do Mouse actions to navigate the screen based on what user asks.
            - You can analyze the screenshot using your own vision and identify UI elements like buttons, text, links, and form fields.
            - If the user asks to "click on something", you must first take a screenshot, analyze it, and then extract the appropriate (x, y) coordinate to perform the action.
            - If coordinates are not explicitly known, infer their position visually using the image provided.

            ---

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

            ---

            🌐 Browser Automation — `ChromePlugin`:

            - `OpenUrl("https://www.google.com")`: Opens a webpage in the default browser.
            - `CloseTab()`: Closes the current browser tab.
            - `RefreshPage()`: Refreshes the page.
            - Always wait or verify page load before interacting.

            ---

            🧠 Reasoning Strategy:

            1. Interpret the user’s goal or command.
            2. Use `ScreenshotPlugin` to visually observe the screen.
            3. Use your vision model to understand where the desired UI element is (button, link, article).
            4. Extract or infer (x, y) from the image or the provided vision response.
            5. Use `MousePlugin` to move and click at that position.
            6. If input is needed, use `KeyboardPlugin.TypeText()` and `KeyboardPlugin.PressKey("Enter")`.
            7. Always take a screenshot after an action to verify the result or determine the next step.

            ---

            🔍 Example Tasks:

            - "Open Google and search for 'latest research in LLMs'" → Open browser → Type → Press Enter → Wait → Screenshot → Vision → Find 2nd article → Click.
            - "Click on the 'Download' button" → Screenshot → Vision → Find position of 'Download' → Click.
            - "Type 'Hello' in the chat box and press Enter" → Screenshot → Find chat box → Click → Type → Press Enter.

            ---

            🎯 Your objective is to behave like a visual agent who *sees*, *thinks*, and *acts*. Be deliberate. Use screenshots to observe, and plugins to interact. Always act based on what’s visible on the screen.

            """
               );
            
            if(i>=1)
             chatHistory.AddUserMessage($"Previous goal: {actions[i-1]}");
            
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();
            chatHistory.AddUserMessage(currentStep);
            request.ChatHistory.Add($"User: {currentStep}");
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

         while (true)
         {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("👤 You: ");
            Console.ResetColor();

            string input = Console.ReadLine()?.Trim() ?? "";

            await ExecutePlannedActionsAsync(input);

            if (string.Equals(input, "quit", StringComparison.OrdinalIgnoreCase))
            {
               Console.ForegroundColor = ConsoleColor.Red;
               Console.WriteLine("👋 Ending chat session...\n");
               Console.ResetColor();
               break;
            }

            if (string.IsNullOrWhiteSpace(input)) continue;


         }

         Console.ForegroundColor = ConsoleColor.Magenta;
         Console.WriteLine("📜 Final Chat History:\n");
         foreach (var line in request.ChatHistory)
         {
            Console.WriteLine(line);
         }
         Console.ResetColor();
      }

      public async Task<List<string>> GetActionStepsAsync(string userGoal)
      {
         var chatService = _kernel.GetRequiredService<IChatCompletionService>();
         var planHistory = new ChatHistory();
         planHistory.AddSystemMessage(
            """
              You are an powerful desktop automation assistant that breaks down a high-level user goal into precise UI actions.

            - If the user goal is simple, provide a single action in an array, for which you think that single verification of screenshot is enough.
           
            - If the user goal is complex, break it down into multiple actions, each with atleast one verification step and requires screenshot.

            - For example:
                - Simple goal: [`"Open Google Chrome and search for Elon Musk"`]
                - Complex goal:
                  [
                    `"Open Google Chrome and search for top AI researchers in 2024"`,
                    `"Scroll through the search results and find an article that matches the category 'research publication'"`
                  ]

            Always return:
            - an array of string/s (if multiple steps with verification are needed).

            Do not return explanations. Output only the prompt(s).
            ");
            

            """);
         OpenAIPromptExecutionSettings settings = new OpenAIPromptExecutionSettings();

         settings = new()
         {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            TopP = 1,
            Temperature = 0.7
         };
         planHistory.AddUserMessage($"User goal: {userGoal}");

         var planResponse = await chatService.GetChatMessageContentAsync(planHistory, settings, _kernel);

         var rawText = planResponse.Content;
         var parsed = JsonSerializer.Deserialize<List<string>>(rawText ?? "[]");

         if (parsed == null || parsed.Count == 0)
         {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("😊 Oops! No actions found.");
            Console.ResetColor();
         }
         else
         {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("🎉 Here are the actions you performed:");
            Console.ResetColor();

            foreach (var action in parsed)
            {
               Console.WriteLine($"👉 {action}");
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("👍 All actions listed above. Keep up the great work!");
            Console.ResetColor();
         }

         return parsed ?? new List<string>();
      }

   }
}
