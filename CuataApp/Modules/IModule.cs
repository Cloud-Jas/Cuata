namespace Cuata.Modules
{
   public interface IModule
   {
      Task RunApp(string? defaultParam = null);
   }
}