public class PresenceState
{
   private static readonly Lazy<PresenceState> _instance = new(() => new PresenceState());
   public static PresenceState Instance => _instance.Value;
   public event Action<bool>? PresenceChanged;
   private bool _isPresent;

   public bool IsPresent
   {
      get => _isPresent;
      set
      {
         if (_isPresent != value)
         {
            _isPresent = value;
            OnPresenceChanged();
         }
      }
   }

   private void OnPresenceChanged()
   {
      Console.WriteLine($"Presence changed: {_isPresent}");
      PresenceChanged?.Invoke(_isPresent);
   }

   private PresenceState() { }
}
