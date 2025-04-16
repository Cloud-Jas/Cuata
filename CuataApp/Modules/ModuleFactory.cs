using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            0 => _serviceProvider.GetService<ChatCompletion>(),
            1 => _serviceProvider.GetService<BrowserAgent>(),
            2 => _serviceProvider.GetService<MultiAgentInteractions>(),
            _ => null
         };
      }
   }
}
