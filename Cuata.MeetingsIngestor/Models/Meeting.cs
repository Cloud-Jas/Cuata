using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cuata.MeetingsIngestor.Models
{
   public class Meeting
   {
      public string id { get; set; }
      public string userId { get; set; }
      public string subject { get; set; }
      public DateTime startTime { get; set; }
      public DateTime endTime { get; set; }
      public string teamsLink { get; set; }
      public bool notified { get; set; }
      public bool cancelled { get; set; }
   }

}
