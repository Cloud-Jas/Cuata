using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cuata.Models
{
   public class MeetingMessage
   {
      public string Subject { get; set; }
      public DateTime StartTime { get; set; }
      public string TeamsLink { get; set; }
      public string UserId { get; set; }
   }

}
