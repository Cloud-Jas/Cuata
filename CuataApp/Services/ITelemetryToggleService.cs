using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cuata.Services
{
   public interface ITelemetryToggleService
   {
      bool IsEnabled { get; }
      void Toggle();
   }

}
