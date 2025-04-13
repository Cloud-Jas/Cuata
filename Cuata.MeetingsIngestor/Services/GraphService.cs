using Microsoft.Graph;
using Cuata.MeetingsIngestor.Models;

namespace Cuata.MeetingsIngestor.Services;
public class GraphService
{
   private readonly GraphServiceClient _client;

   public GraphService(GraphServiceClient client)
   {
      _client = client;
   }

   public async Task<List<Meeting>> GetTodayMeetingsAsync(string userId)
   {
      var startOfDay = DateTime.UtcNow.Date;
      var endOfDay = startOfDay.AddDays(1);

      try
      {
         var eventsResult = await _client.Users[userId]
             .CalendarView
             .GetAsync(requestConfig =>
             {
                requestConfig.QueryParameters.StartDateTime = startOfDay.ToString("o");
                requestConfig.QueryParameters.EndDateTime = endOfDay.ToString("o");
                requestConfig.QueryParameters.Select = new[] { "id", "subject", "start", "end", "onlineMeeting", "isOnlineMeeting" };
                requestConfig.QueryParameters.Top = 100;
                requestConfig.Headers.Add("Prefer", "outlook.timezone=\"UTC\"");
             });

         if (eventsResult?.Value == null) return new List<Meeting>();

         return eventsResult.Value
             .Where(e =>
                 e.IsOnlineMeeting == true &&
                 e.OnlineMeeting != null &&
                 !string.IsNullOrEmpty(e.OnlineMeeting.JoinUrl) &&
                 e.OnlineMeeting.JoinUrl.Contains("teams.microsoft.com"))
             .Select(e => new Meeting
             {
                id = e.Id,
                subject = e.Subject,
                startTime = DateTime.TryParse(e.Start?.DateTime, out var start) ? start : DateTime.MinValue,
                endTime = DateTime.TryParse(e.End?.DateTime, out var end) ? end : DateTime.MinValue,
                teamsLink = e.OnlineMeeting.JoinUrl,
                userId = userId
             })
             .ToList();
      }
      catch (Exception ex)
      {
         Console.WriteLine($"Error fetching meetings: {ex.Message}");
         return new List<Meeting>();
      }
   }
}
