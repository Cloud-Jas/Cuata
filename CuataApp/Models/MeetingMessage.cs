using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace Cuata.Models
{
   public class MeetingMessage
   {
      [JsonProperty("subject")]
      public string Subject { get; set; }
      [JsonProperty("startTime")]
      public DateTime StartTime { get; set; }
      [JsonProperty("teamsLink")]
      public string TeamsLink { get; set; }
      [JsonProperty("userId")]
      public string UserId { get; set; }
      [JsonProperty("id")]
      public string Id { get; set; }
   }

}
