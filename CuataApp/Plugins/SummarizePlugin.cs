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
using WindowsInput;

public class SummarizePlugin
{
   private readonly Kernel _kernel;
   private readonly InputSimulator _inputSimulator = new InputSimulator();
   public SummarizePlugin(Kernel kernel)
   {
      _kernel = kernel;
   }
   public async Task<string> TakeScreenshotTillEndOfThePage()
   {
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

      Console.ForegroundColor = ConsoleColor.Cyan;
      Console.WriteLine($"🖼️ Screenshot saved to: {outputActualPath}");

      return outputActualPath;
   }

   [KernelFunction, Description("Summarize or get the content of the current page/screen")]
   public async Task<string> SummarizePage([Description("`true` for entire page, `false` for current page ")] bool isFullScreen)
   {
      var imagePaths = new List<string>();

      if (isFullScreen)
      {
         for (int i = 0; i < 5; i++)
         {
            _inputSimulator.Mouse.VerticalScroll(-10);
            var direction = -10 > 0 ? "up" : "down";
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"🖱️ Scrolled the mouse {direction} by {Math.Abs(-10)} click(s)!");
            Console.ResetColor();

            Thread.Sleep(1500);
            var imagePath = await TakeScreenshotTillEndOfThePage();
            imagePaths.Add(imagePath);
         }
      }
      else
      {
         var imagePath = await TakeScreenshotTillEndOfThePage();
         imagePaths.Add(imagePath);
      }
      var chatService = _kernel.GetRequiredService<IChatCompletionService>();

      var chatMessage = new ChatMessageContentItemCollection
    {
        new TextContent("Please summarize the content of the current page/screen in 3 paragraphs.")
    };

      foreach (var imagePath in imagePaths)
      {
         using var img = new Bitmap(imagePath);
         using var ms = new MemoryStream();
         img.Save(ms, ImageFormat.Png);
         ms.Position = 0;
         var imageData = new ReadOnlyMemory<byte>(ms.ToArray());
         chatMessage.Add(new ImageContent(imageData, "image/png"));
      }

      var history = new ChatHistory();
      history.AddUserMessage(chatMessage);

      Console.ForegroundColor = ConsoleColor.Cyan;
      Console.WriteLine("📝 Summarizing page...");
      Console.ResetColor();

      var result = await chatService.GetChatMessageContentsAsync(history);
      var message = result.FirstOrDefault()?.Content?.Trim() ?? "No response from model.";

      return message;
   }
}
