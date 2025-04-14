using Microsoft.SemanticKernel;
using System.ComponentModel;
using WindowsInput;
using WindowsInput.Native;

namespace Cuata.Plugins
{
   public class MousePlugin
   {
      private readonly InputSimulator _inputSimulator = new InputSimulator();

      [KernelFunction, Description("Moves the mouse to the specified screen coordinates.")]
      public void MoveMouse([Description("The X coordinate.")] int x, [Description("The Y coordinate.")] int y, [Description("The screen width.")] int screenWidth, [Description("The screen height.")] int screenHeight)
      {

         Console.WriteLine($"🖱️ Moving the mouse to coordinates ({x}, {y}) on a screen of size ({screenWidth}, {screenHeight})");

         double X = x * 65535 / 2560;
         double Y = y * 65535 / 1600;

         _inputSimulator.Mouse.MoveMouseTo(X, Y);
         Console.ForegroundColor = ConsoleColor.Cyan;
         Console.WriteLine($"🖱️ Moved the mouse to coordinates ({x}, {y})");
         Console.ResetColor();
      }

      [KernelFunction, Description("Performs a left mouse click.")]
      public void LeftClick()
      {
         _inputSimulator.Mouse.LeftButtonClick();
         Console.ForegroundColor = ConsoleColor.Green;
         Console.WriteLine("🖱️ Left mouse button clicked! ✅");
         Console.ResetColor();
      }

      [KernelFunction, Description("Performs a right mouse click.")]
      public void RightClick()
      {
         _inputSimulator.Mouse.RightButtonClick();
         Console.ForegroundColor = ConsoleColor.Magenta;
         Console.WriteLine("🖱️ Right mouse button clicked! ✅");
         Console.ResetColor();
      }

      [KernelFunction, Description("Scrolls the mouse wheel by a given number of scroll clicks.")]
      public void Scroll([Description("The amount to scroll. Positive to scroll up, negative to scroll down.")] int scrollAmount)
      {
         _inputSimulator.Mouse.VerticalScroll(scrollAmount);
         var direction = scrollAmount > 0 ? "up" : "down";
         Console.ForegroundColor = ConsoleColor.Yellow;
         Console.WriteLine($"🖱️ Scrolled the mouse {direction} by {Math.Abs(scrollAmount)} click(s)!");
         Console.ResetColor();
      }

      [KernelFunction, Description("Presses and holds the left mouse button.")]
      public void LeftButtonDown()
      {
         _inputSimulator.Mouse.LeftButtonDown();
         Console.ForegroundColor = ConsoleColor.Red;
         Console.WriteLine("🖱️ Left mouse button is being held down...");
         Console.ResetColor();
      }

      [KernelFunction, Description("Releases the left mouse button.")]
      public void LeftButtonUp()
      {
         _inputSimulator.Mouse.LeftButtonUp();
         Console.ForegroundColor = ConsoleColor.Red;
         Console.WriteLine("🖱️ Left mouse button released! 🎉");
         Console.ResetColor();
      }

      [KernelFunction, Description("Presses and holds the right mouse button.")]
      public void RightButtonDown()
      {
         _inputSimulator.Mouse.RightButtonDown();
         Console.ForegroundColor = ConsoleColor.Red;
         Console.WriteLine("🖱️ Right mouse button is being held down...");
         Console.ResetColor();
      }

      [KernelFunction, Description("Releases the right mouse button.")]
      public void RightButtonUp()
      {
         _inputSimulator.Mouse.RightButtonUp();
         Console.ForegroundColor = ConsoleColor.Red;
         Console.WriteLine("🖱️ Right mouse button released! 🎉");
         Console.ResetColor();
      }
   }
}
