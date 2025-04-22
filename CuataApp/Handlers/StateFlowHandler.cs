using Cuata.Modules;

namespace Cuata.Handlers
{
   public class StateFlowHandler
   {
      private readonly TeamsAgent _teamsAgent;
      private readonly ScreenshotService _screenshotService;

      public StateFlowHandler(TeamsAgent agent, ScreenshotService screenshotService)
      {
         _teamsAgent = agent;
         _screenshotService = screenshotService;

         CuataState.Instance.OnMeetingStatusChanged += OnMeetingStatusChanged;
         CuataState.Instance.OnPresenceChanged += OnPresenceChanged;
      }

      private async void OnMeetingStatusChanged(bool isMeeting)
      {
         if (isMeeting)
         {
            if (!CuataState.Instance.IsPresent)
            {
               Console.WriteLine("👻 User not present. Triggering RunApp...");
               await _teamsAgent.RunApp(CuataState.Instance.MeetingTitle);
               _screenshotService.Start(CuataState.Instance.MeetingTitle!, CuataState.Instance.MeetingId!);
               CuataState.Instance.IsScreenshotRunning = true;
            }
         }
         else
         {
            if (CuataState.Instance.IsScreenshotRunning)
            {
               _screenshotService.Stop();
               await _screenshotService.ConsolidateSummaryAsync();
               Console.WriteLine("📧 You’ll receive a summary in a moment.");
            }
         }
      }

      private async void OnPresenceChanged(bool isPresent)
      {
         if (CuataState.Instance.IsMeetingOngoing)
         {
            if (!isPresent && !_screenshotService.IsRunning)
            {
               Console.WriteLine("🛑 User left during meeting. Starting screenshot loop...");
               await _teamsAgent.RunApp(CuataState.Instance.MeetingTitle);
               _screenshotService.Start(CuataState.Instance.MeetingTitle! ,CuataState.Instance.MeetingId! );
               CuataState.Instance.IsScreenshotRunning = true;
            }
            else if (isPresent && _screenshotService.IsRunning)
            {
               Console.WriteLine("✅ User returned. Stopping screenshot loop.");
               _screenshotService.Stop();
               CuataState.Instance.IsScreenshotRunning = false;
               await _screenshotService.ConsolidateSummaryAsync();
               Console.WriteLine("📧 You’ll receive a summary in a moment.");
            }
         }
      }
   }

}
