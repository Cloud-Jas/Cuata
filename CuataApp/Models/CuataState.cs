using Cuata.Models;

public class CuataState
{
   private static readonly Lazy<CuataState> _instance = new(() => new CuataState());
   public static CuataState Instance => _instance.Value;

   public event Action? StateChanged;
   public event Action<bool>? OnPresenceChanged;
   public event Action<bool>? OnMeetingStatusChanged;
   public event Action<string?>? OnMeetingTitleChanged;
   public event Action<bool>? OnScreenshotRunningChanged;
   public event Action<ConsolidatedSummaryResult?>? OnConsolidatedSummaryChanged;

   private bool _isPresent;
   private bool _isMeetingOngoing;
   private string? _meetingTitle;
   private string? _meetingId;
   private bool _isScreenshotRunning;

   public bool IsPresent
   {
      get => _isPresent;
      set
      {
         if (_isPresent != value)
         {
            _isPresent = value;
            OnPresenceChanged?.Invoke(value);
            StateChanged?.Invoke();
         }
      }
   }

   public bool IsMeetingOngoing
   {
      get => _isMeetingOngoing;
      set
      {
         if (_isMeetingOngoing != value)
         {
            _isMeetingOngoing = value;
            OnMeetingStatusChanged?.Invoke(value);
            StateChanged?.Invoke();
         }
      }
   }

   public string? MeetingId
   {
      get => _meetingId;
      set
      {
         if (_meetingId != value)
         {
            _meetingId = value;
            StateChanged?.Invoke();
         }
      }
   }

   public string? MeetingTitle
   {
      get => _meetingTitle;
      set
      {
         if (_meetingTitle != value)
         {
            _meetingTitle = value;
            OnMeetingTitleChanged?.Invoke(value);
            StateChanged?.Invoke();
         }
      }
   }

   public bool IsScreenshotRunning
   {
      get => _isScreenshotRunning;
      set
      {
         if (_isScreenshotRunning != value)
         {
            _isScreenshotRunning = value;
            OnScreenshotRunningChanged?.Invoke(value);
            StateChanged?.Invoke();
         }
      }
   }

   private ConsolidatedSummaryResult? _consolidatedSummary;
   public ConsolidatedSummaryResult? ConsolidatedSummary
   {
      get => _consolidatedSummary;
      set
      {
         if (_consolidatedSummary != value)
         {
            _consolidatedSummary = value;
            OnConsolidatedSummaryChanged?.Invoke(value);
            StateChanged?.Invoke();
         }
      }
   }


   private CuataState() { }
}
