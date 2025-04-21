using Cuata.Modules;
using Cuata.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
               await _teamsAgent.RunApp(CuataState.Instance.MeetingTitle ?? "Meeting");
            }
         }
         else
         {
            if (CuataState.Instance.IsScreenshotRunning)
            {
               _screenshotService.Stop();
               Console.WriteLine("📧 You’ll receive a summary mail of what happened.");
            }
         }
      }

      private void OnPresenceChanged(bool isPresent)
      {
         if (CuataState.Instance.IsMeetingOngoing)
         {
            if (!isPresent && !_screenshotService.IsRunning)
            {
               Console.WriteLine("🛑 User left during meeting. Starting screenshot loop...");
               _screenshotService.Start(CuataState.Instance.MeetingTitle ?? "Meeting",CuataState.Instance.MeetingId ?? "AAMkAGQ5YzgxOWJiLWNmMWUtNDg2MC1hZjRkLWE0YzhhZWI0Y2FkZABGAAAAAABsg8Kv6SHRSYg2zYOaHUcZBwDNZm03iP_kRoijVOnlvXohAAAAAAENAADNZm03iP_kRoijVOnlvXohAABDyxbmAAA=");
               CuataState.Instance.IsScreenshotRunning = true;
            }
            else if (isPresent && _screenshotService.IsRunning)
            {
               Console.WriteLine("✅ User returned. Stopping screenshot loop.");
               _screenshotService.Stop();
               CuataState.Instance.IsScreenshotRunning = false;
               Console.WriteLine("📧 You’ll receive a summary mail of what happened.");
            }
         }
      }
   }

}
