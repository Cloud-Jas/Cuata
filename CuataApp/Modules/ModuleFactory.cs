using Microsoft.Extensions.DependencyInjection;

namespace Cuata.Modules
{
   public class ModuleFactory
   {
      private readonly IServiceProvider _serviceProvider;

      public ModuleFactory(IServiceProvider serviceProvider)
      {
         _serviceProvider = serviceProvider;
      }

      public IModule? GetModule(int selectedIndex)
      {
         return selectedIndex switch
         {
            0 => _serviceProvider.GetService<TeamsAgent>(),
            1 => _serviceProvider.GetService<BrowserAgent>(),
            _ => null
         };
      }
   }
}
