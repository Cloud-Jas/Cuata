using Microsoft.SemanticKernel;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using WindowsInput;
using WindowsInput.Native;

namespace Cuata.Plugins
{
   public class WordPlugin
   {
      private readonly InputSimulator _input = new();

      private string GetMicrosoftWordPath()
      {
         var paths = new[]
         {
            Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\Microsoft Office\root\Office16\WINWORD.EXE"),
            Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Microsoft Office\root\Office16\WINWORD.EXE"),
            Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\Microsoft Office\root\Office15\WINWORD.EXE"),
            Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Microsoft Office\root\Office15\WINWORD.EXE")
         };

         foreach (var path in paths)
         {
            if (File.Exists(path))
               return path;
         }

         throw new FileNotFoundException("Word not found in default install locations.");
      }

      [KernelFunction, Description("Open Microsoft Word with a new document.")]
      public string OpenWord()
      {
         try
         {
            string wordPath = GetMicrosoftWordPath();
            Process.Start(wordPath);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("📄 Microsoft Word opened successfully.");
            Console.ResetColor();
            return "Opened Microsoft Word.";
         }
         catch (Exception ex)
         {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ Failed to open Microsoft Word: {ex.Message}");
            Console.ResetColor();
            return $"Failed to open Microsoft Word: {ex.Message}";
         }
      }

      [KernelFunction, Description("Close the current Microsoft Word document using Ctrl+W shortcut.")]
      public string CloseWord()
      {
         _input.Keyboard.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_W);
         Console.ForegroundColor = ConsoleColor.Magenta;
         Console.WriteLine("🧹 Closed the current Microsoft Word document.");
         Console.ResetColor();
         return "Closed current Microsoft Word document.";
      }

      [KernelFunction, Description("Save the current Microsoft Word document using Ctrl+S shortcut.")]
      public string ReadWord()
      {
         _input.Keyboard.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_S);
         Console.ForegroundColor = ConsoleColor.Yellow;
         Console.WriteLine("💾 Saved the current Microsoft Word document.");
         Console.ResetColor();
         return "Saved current Microsoft Word document.";
      }

      [KernelFunction, Description("Print the current Microsoft Word document using Ctrl+P shortcut.")]
      public string WriteWord()
      {
         _input.Keyboard.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_P);
         Console.ForegroundColor = ConsoleColor.Cyan;
         Console.WriteLine("🖨️ Printed the current Microsoft Word document.");
         Console.ResetColor();
         return "Printed current Microsoft Word document.";

      }

   }
}
