using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cuata.Models
{
   public class RequestData
   {
      public RequestData()
      {

      }
      public string? UserQuery { get; set; }
      public string? AssistantMessage { get; set; }
      public List<string>? ChatHistory { get; set; }
      public string? Mode { get; set; }
   }
}
