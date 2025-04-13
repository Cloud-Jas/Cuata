using Microsoft.SemanticKernel;
using System;
using System.ComponentModel;

namespace Cuata.Plugins
{
   public class TimePlugin
   {
      [KernelFunction, Description("Gets the current time.")]
      public string GetCurrentTime()
      {
         return DateTime.Now.ToString("HH:mm:ss");
      }

      [KernelFunction, Description("Gets the current date.")]
      public string GetCurrentDate()
      {
         return DateTime.Now.ToString("yyyy-MM-dd");
      }
   }
}
