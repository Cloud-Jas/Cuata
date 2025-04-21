using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cuata.Modules
{
   public class TextElement
   {
      public string Text { get; set; }
      public List<PointF> BoundingBox { get; set; }
   }

}
