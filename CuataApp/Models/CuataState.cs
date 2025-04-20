public class CuataState
{
   private static readonly Lazy<CuataState> _instance = new(() => new CuataState());
   public static CuataState Instance => _instance.Value;
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

   private CuataState() { }
}
