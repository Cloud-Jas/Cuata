using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.Drawing;
using System.Runtime.InteropServices;
using Cuata;
using System.Diagnostics;
using Cuata.Services;
using Microsoft.Extensions.DependencyInjection;

public class ScreenshotPlugin
{
   [DllImport("user32.dll")]
   private static extern int GetSystemMetrics(int nIndex);
   private const int SM_XVIRTUALSCREEN = 76;
   private const int SM_YVIRTUALSCREEN = 77;
   private const int SM_CXVIRTUALSCREEN = 78;
   private const int SM_CYVIRTUALSCREEN = 79;
   private readonly Kernel _kernel;

   public ScreenshotPlugin(Kernel kernel)
   {
      _kernel = kernel;
   }

   [KernelFunction, Description("Takes a screenshot and verifies the action")]
   public async Task<string> TakeScreenshotAndVerifyAsync(
    [Description("The action to verify")] string action)
   {
      var chatService = _kernel.GetRequiredService<IChatCompletionService>();

      ScreenCapture sc = new ScreenCapture();
      Image img = sc.CaptureScreen();
      int screenWidth = img.Width;
      int screenHeight = img.Height;
      using var bmp = new Bitmap(img);
      using var ms = new MemoryStream();
      bmp.Save(ms, ImageFormat.Png);
      ms.Position = 0;
      var outputActualPath = Path.Combine(Directory.GetCurrentDirectory(), $"screenshot-{DateTime.Now:yyyyMMddHHmmss}.png");
      bmp.Save(outputActualPath, ImageFormat.Png);
      var imageData = new ReadOnlyMemory<byte>(ms.ToArray());

      var items = new ChatMessageContentItemCollection
    {
        new TextContent($"I just performed the following action: \"{action}\". If the action is to search for something, see if it is done and if it is realted to page like Wikipedia, search if you are at the right page by checking address bar with 'wikipedia.org'. Answer with 'Yes' or 'No' and explain."),
        new ImageContent(imageData, "image/png")
    };

      var history = new ChatHistory();
      history.AddUserMessage(items);

      Console.ForegroundColor = ConsoleColor.Cyan;
      Console.WriteLine($"📸 Verifying action {action}...");
      Console.ResetColor();

      var result = await chatService.GetChatMessageContentsAsync(history);
      var message = result.FirstOrDefault()?.Content?.Trim() ?? "No response from model.";

      Console.ForegroundColor = message.StartsWith("Yes", StringComparison.OrdinalIgnoreCase)
          ? ConsoleColor.Green
          : ConsoleColor.Red;
      Console.WriteLine($"{(message.StartsWith("Yes", StringComparison.OrdinalIgnoreCase) ? "✅ Success" : "❌ Failed")}: {message}");

      Console.ResetColor();
      return message;
   }
}
