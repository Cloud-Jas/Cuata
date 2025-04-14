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
using Microsoft.CognitiveServices.Speech;

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
      private readonly SpeechRecognizer _recognizer;

      public CuaAgent(
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

      public async Task ExecutePlannedActionsAsync(string userGoal)
      {

         var actions = await GetActionStepsAsync(userGoal);

         if(_kernel.Plugins.Count > 0)
         {
            _kernel.Plugins.Clear();
         }

         _kernel.ImportPluginFromObject(new TimePlugin(), "TimePlugin");
         _kernel.ImportPluginFromObject(new KeyboardPlugin(), "KeyboardPlugin");
         _kernel.ImportPluginFromObject(new MousePlugin(), "MousePlugin");
         _kernel.ImportPluginFromObject(new ChromePlugin(), "ChromePlugin");
         _kernel.ImportPluginFromObject(new ScreenshotPlugin(_kernel), "ScreenshotPlugin");
         _kernel.ImportPluginFromObject(new LocatePlugin(_kernel, _serviceProvider), "LocatePlugin");


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

            You have access to the following plugins:

            ---

            Chrome Plugin — `ChromePlugin`:
            - `OpenUrl("https://www.google.com")`: Opens a webpage in the default browser.

            🖼️ Locate In Screenshot Actions — `LocatePlugin`:

            - `LocateElementInScreenshot("search text")`: Locates the specified text in the screenshot and returns the coordinates of the UI element in the format X,Y.

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

            ---

            🌐 Browser Automation — `ChromePlugin`:

            - `OpenUrl("https://www.google.com")`: Opens a webpage in the default browser.
            - `CloseTab()`: Closes the current browser tab.
            - `RefreshPage()`: Refreshes the page.
            - Always wait or verify page load before interacting.

            ---

            🧠 Reasoning Strategy:

            1. Act like a human: observe the screen, think, and act.
            2. Use screenshots to verify actions and analyze UI elements.
            3. Use plugins to perform actions based on observations.
            4. Always reason based on the current screen and context.
            5. If you are not sure about the next action, ask for clarification.

            ---

            🔍 Example Tasks:

            Here a helpful example:

            Example 1: Searches for Stock Market news 

            - Take a screenshot of the current screen to see if we are already on the right page, if yes just proceed to next step
            - If not, open Google Chrome
            - Don't locate to type text in the google search box, just type the text in the search box
            - Type "Stock Market news" in the search box and press enter
            - Take a screenshot of the page to verify if we done the previous action correctly
                 
            Example 2: Search for Elon Musk and find his Wikipedia page
            
            - Take a screenshot of the current screen to see if we are already on the right page, if yes just proceed to next step
            - If not, open Google Chrome
            - Don't locate to type text in the google search box, just type the text in the search box
            - Type "Elon Musk" in the search box and press enter
            - Take a screenshot of the page to verify if we done the previous action correctly
            - Scroll through the articles and find if there is a Wikipedia page for Elon Musk and click on it
            - Locate the Wikipedia page and click on it
            - If not found scroll through the articles and find if there is a Wikipedia page for Elon Musk and click on it
            - Take a screenshot of the page to verify if we done the previous action correctly

            Example 3: Search for Motherson Sumi and find any article related to it
            - Take a screenshot of the current screen to see if we are already on the right page, if yes just proceed to next step
            - If not, open Google Chrome
            - Don't locate to type text in the google search box, just type the text in the search box
            - Type "Motherson Sumi" in the search box and press enter
            - Take a screenshot of the page to verify if we done the previous action correctly
            - Locate the articles related to Motherson Sumi and click on it
            - Scroll through the articles and find if there are any articles related to Motherson Sumi
            - Take a screenshot of the page to verify if we done the previous action correctly

            Example 4: Search for a specific article and find the link to it
            
            - Take a screenshot of the current screen to see if we are already on the right page, if yes just proceed to next step
            - If not, open Google Chrome
            - Don't locate to type text in the google search box, just type the text in the search box
            - Locate specific article keywords in the page result and press enter
            - Take a screenshot of the page to verify if we done the previous action correctly
            - Scroll through the articles and find if there are any articles related to Motherson Sumi

            ---

            🎯 Your objective is to behave like a visual agent who *sees*, *thinks*, and *acts*. Be deliberate. Use screenshots to observe, and plugins to interact. Always act based on what’s visible on the screen.

            """
               );

            if (i >= 1)
               chatHistory.AddUserMessage($"Previous goal: {actions[i - 1]}");

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
         Console.WriteLine("🎤 Speak to the AI. Say 'quit' to exit.\n");
         Console.ResetColor();
         Console.WriteLine("🎧 Starting continuous recognition...\n");
         await _recognizer.StartContinuousRecognitionAsync();

         _recognizer.Recognized += async (s, e) =>
         {
            if (e.Result.Reason == ResultReason.RecognizedSpeech)
            {
               string input = e.Result.Text.Trim();
               Console.ForegroundColor = ConsoleColor.Yellow;
               Console.WriteLine($"✔ Final recognized: {input}");
               Console.ResetColor();
               
               if (string.IsNullOrWhiteSpace(input)) return;

               if (input.Equals("quit", StringComparison.OrdinalIgnoreCase))
               {
                  Console.ForegroundColor = ConsoleColor.Red;
                  Console.WriteLine("🛑 Stopping recognition...");
                  Console.ResetColor();
                  await _recognizer.StopContinuousRecognitionAsync();
               }
               else
               {
                  await ExecutePlannedActionsAsync(input);
               }
            }
         };

         _recognizer.Canceled += (s, e) =>
         {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ Canceled: {e.Reason}");
            Console.ResetColor();
         };

         _recognizer.SessionStopped += async (s, e) =>
         {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("🔚 Session stopped.");
            Console.ResetColor();
         };

         await Task.Delay(Timeout.Infinite);

      }

      public async Task<List<string>> GetActionStepsAsync(string userGoal)
      {
         var chatService = _kernel.GetRequiredService<IChatCompletionService>();
         var planHistory = new ChatHistory();
         planHistory.AddSystemMessage(
            """
              You are an powerful desktop automation assistant that breaks down a high-level user goal into precise UI actions.

            - If the user goal is simple, provide a single action in an array.
            - Your thought process should be clear and concise in such a way that each action should have only one verification step.
            - If the user goal is complex, break it down into multiple actions, each with atleast one verification step and requires screenshot.

            🔍 Example Tasks:
            
            Here a helpful example:
            
            Example 1: Searches for Stock Market news and find any article related to Motherson Sumi
            [
                {`"open Google Chrome to search for stock market news and type "Stock Market news" in the search box and press enter"`},
                {`"scroll through the articles and find if there are any articles related to Motherson sumi"` }
            ]
            
            Example 2: Search for Elon Musk and find his Wikipedia page
            [
                {`"open Google Chrome to search for "Elon Musk" and type "Elon Musk" in the search box and press enter"`},
                {`"scroll through the articles and find if there is a Wikipedia page for Elon Musk and click on it"` }
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
            Temperature = 0.4
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
