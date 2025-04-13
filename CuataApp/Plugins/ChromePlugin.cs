using Microsoft.SemanticKernel;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using WindowsInput;
using WindowsInput.Native;

namespace Cuata.Plugins
{
   public class ChromePlugin
   {
      private readonly InputSimulator _input = new();

      private string GetChromePath()
      {
         var paths = new[]
         {
                Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe"),
                Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Google\Chrome\Application\chrome.exe")
            };

         foreach (var path in paths)
         {
            if (File.Exists(path))
               return path;
         }

         throw new FileNotFoundException("Chrome not found in default install locations.");
      }

      [KernelFunction, Description("Opens the specified URL in Google Chrome.")]
      public string OpenUrl(string url)
      {
         try
         {
            string chromePath = GetChromePath();
            Process.Start(chromePath, url);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"🌐 Successfully opened: {url}");
            Console.ResetColor();
            return $"Opened {url} in Chrome.";
         }
         catch (Exception ex)
         {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ Failed to open Chrome: {ex.Message}");
            Console.ResetColor();
            return $"Failed to open Chrome: {ex.Message}";
         }
      }

      [KernelFunction, Description("Closes the current Chrome tab using Ctrl+W shortcut.")]
      public string CloseTab()
      {
         _input.Keyboard.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_W);
         Console.ForegroundColor = ConsoleColor.Magenta;
         Console.WriteLine("🧹 Closed the current Chrome tab.");
         Console.ResetColor();
         return "Closed current Chrome tab.";
      }

      [KernelFunction, Description("Refreshes the current Chrome tab using F5.")]
      public string RefreshPage()
      {
         _input.Keyboard.KeyPress(VirtualKeyCode.F5);
         Console.ForegroundColor = ConsoleColor.Cyan;
         Console.WriteLine("🔄 Refreshed the current Chrome tab.");
         Console.ResetColor();
         return "Refreshed the page.";
      }

      [KernelFunction, Description("Opens a new tab in Chrome using Ctrl+T.")]
      public string OpenNewTab()
      {
         _input.Keyboard.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_T);
         Console.ForegroundColor = ConsoleColor.Blue;
         Console.WriteLine("🆕 Opened a new tab in Chrome.");
         Console.ResetColor();
         return "Opened a new Chrome tab.";
      }

      [KernelFunction, Description("Goes back in Chrome using Alt+Left Arrow.")]
      public string GoBack()
      {
         _input.Keyboard.ModifiedKeyStroke(VirtualKeyCode.MENU, VirtualKeyCode.LEFT);
         Console.ForegroundColor = ConsoleColor.Yellow;
         Console.WriteLine("⬅️ Went back in Chrome.");
         Console.ResetColor();
         return "Navigated back in Chrome.";
      }

      [KernelFunction, Description("Goes forward in Chrome using Alt+Right Arrow.")]
      public string GoForward()
      {
         _input.Keyboard.ModifiedKeyStroke(VirtualKeyCode.MENU, VirtualKeyCode.RIGHT);
         Console.ForegroundColor = ConsoleColor.Yellow;
         Console.WriteLine("➡️ Went forward in Chrome.");
         Console.ResetColor();
         return "Navigated forward in Chrome.";
      }

   }
}
