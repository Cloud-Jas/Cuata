using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using WindowsInput;

namespace Cuata.Plugins
{
   public class TeamsPlugin
   {
      private readonly InputSimulator _input = new();
      private const string TeamsUri = "msteams:work";

      [KernelFunction, Description("Opens Microsoft Teams.")]
      public string OpenTeams()
      {
         try
         {
            Process.Start(new ProcessStartInfo
            {
               FileName = TeamsUri,
               UseShellExecute = true
            });

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("🌐 Successfully opened Microsoft Teams.");
            Console.ResetColor();
            return "Opened Microsoft Teams.";
         }
         catch (Exception ex)
         {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ Failed to open Microsoft Teams: {ex.Message}");
            Console.ResetColor();
            return $"Failed to open Microsoft Teams: {ex.Message}";
         }
      }

   }
}
