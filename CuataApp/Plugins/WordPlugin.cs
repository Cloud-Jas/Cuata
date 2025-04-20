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
      public async Task<string> OpenWord()
      {
         try
         {
            string wordPath = GetMicrosoftWordPath();
            Process.Start(wordPath);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("📄 Microsoft Word opened successfully."); 
            Console.ResetColor();
            await Task.Delay(2000);
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

      [KernelFunction, Description("Writes summary of the page into Word document")]
      public async Task<string> WriteWord([Description("Sumamry of the page")]string summary)
      {
         await Task.Delay(2000);
         _input.Keyboard.TextEntry(summary);
         _input.Keyboard.KeyPress(VirtualKeyCode.RETURN);
         Console.ForegroundColor = ConsoleColor.Cyan;
         Console.WriteLine($"📝 Wrote summary into Word document. {summary}");
         Console.ResetColor();
         return "Wrote summary into Word document.";
      }

   }
}
