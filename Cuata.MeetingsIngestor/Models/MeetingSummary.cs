using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cuata.MeetingsIngestor.Models
{
   public class MeetingSummary
   {
      public string id { get; set; }
      public string meetingId { get; set; }
      public string userId { get; set; }
      public string imageUrl { get; set; }
      public string summary { get; set; }
      public DateTime timestamp { get; set; }
   }

}
