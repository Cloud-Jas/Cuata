using Microsoft.SemanticKernel;
using System.ComponentModel;
using WindowsInput;
using WindowsInput.Native;

namespace Cuata.Plugins
{
   public class KeyboardPlugin
   {
      private readonly InputSimulator _inputSimulator = new InputSimulator();

      [KernelFunction, Description("Types the given text using the keyboard.")]
      public void TypeText([Description("The text to type.")] string text)
      {
         _inputSimulator.Keyboard.TextEntry(text);
         Console.ForegroundColor = ConsoleColor.Green;
         Console.WriteLine($"⌨️ Typed the text: {text}");
         Console.ResetColor();
      }

      [KernelFunction, Description("Presses a single key (no modifier).")]
      public void PressKey([Description("The key to press, e.g., 'Enter', 'A', 'Tab'.")] string key)
      {
         var virtualKey = ParseKey(key);
         _inputSimulator.Keyboard.KeyPress(virtualKey);
         Console.ForegroundColor = ConsoleColor.Cyan;
         Console.WriteLine($"🔑 Pressed the key: {key}");
         Console.ResetColor();
      }

      [KernelFunction, Description("Holds down a key.")]
      public void KeyDown([Description("The key to hold, e.g., 'Shift', 'Ctrl', 'A'.")] string key)
      {
         var virtualKey = ParseKey(key);
         _inputSimulator.Keyboard.KeyDown(virtualKey);
         Console.ForegroundColor = ConsoleColor.Yellow;
         Console.WriteLine($"⚡️ Holding down the key: {key}");
         Console.ResetColor();
      }

      [KernelFunction, Description("Releases a held key.")]
      public void KeyUp([Description("The key to release, e.g., 'Shift', 'Ctrl', 'A'.")] string key)
      {
         var virtualKey = ParseKey(key);
         _inputSimulator.Keyboard.KeyUp(virtualKey);
         Console.ForegroundColor = ConsoleColor.Magenta;
         Console.WriteLine($"👐 Released the key: {key}");
         Console.ResetColor();
      }

      [KernelFunction, Description("Simulates pressing a combination of keys (e.g., Ctrl+C).")]
      public void PressKeyCombo(
          [Description("The modifier key, e.g., 'Ctrl', 'Shift'.")] string modifier,
          [Description("The key to combine with modifier, e.g., 'C', 'V'.")] string key)
      {
         var modKey = ParseKey(modifier);
         var mainKey = ParseKey(key);
         _inputSimulator.Keyboard.ModifiedKeyStroke(modKey, mainKey);
         Console.ForegroundColor = ConsoleColor.Green;
         Console.WriteLine($"🔑 Pressed combination: {modifier}+{key}");
         Console.ResetColor();
      }

      private VirtualKeyCode ParseKey(string key)
      {
         key = key.Trim().ToUpperInvariant();

         return key switch
         {
            "ENTER" => VirtualKeyCode.RETURN,
            "TAB" => VirtualKeyCode.TAB,
            "ESC" or "ESCAPE" => VirtualKeyCode.ESCAPE,
            "SHIFT" => VirtualKeyCode.SHIFT,
            "CTRL" or "CONTROL" => VirtualKeyCode.CONTROL,
            "ALT" => VirtualKeyCode.MENU,
            "BACKSPACE" => VirtualKeyCode.BACK,
            "SPACE" => VirtualKeyCode.SPACE,
            "F4" => VirtualKeyCode.F4,
            "Win" => VirtualKeyCode.LWIN,
            "WIN" => VirtualKeyCode.LWIN,
            "UP" => VirtualKeyCode.UP,
            "Up" => VirtualKeyCode.UP,
            _ when key.Length == 1 && char.IsLetterOrDigit(key[0]) =>
                (VirtualKeyCode)Enum.Parse(typeof(VirtualKeyCode), $"VK_{key[0]}"),
            _ => throw new ArgumentException($"Unsupported or invalid key: {key}")
         };
      }
   }
}
