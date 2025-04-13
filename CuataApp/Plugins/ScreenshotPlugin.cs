using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.Drawing;
using System.Runtime.InteropServices;

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

   [KernelFunction, Description("Takes a screenshot and either verifies a past action or identifies coordinates of an element described.")]
   public async Task<string> TakeScreenshotAndAnalyzeAsync(
    [Description("The purpose: 'verify' to confirm an action, or 'locate' to find something on screen.")] string mode,
    [Description("The action to verify or the UI description to locate.")] string input)
   {
      var chatService = _kernel.GetRequiredService<IChatCompletionService>();

      int screenLeft = GetSystemMetrics(SM_XVIRTUALSCREEN);
      int screenTop = GetSystemMetrics(SM_YVIRTUALSCREEN);
      int screenWidth = GetSystemMetrics(SM_CXVIRTUALSCREEN);
      int screenHeight = GetSystemMetrics(SM_CYVIRTUALSCREEN);

      using var bmp = new Bitmap(screenWidth, screenHeight);
      using var g = Graphics.FromImage(bmp);
      g.CopyFromScreen(0, 0, 0, 0, bmp.Size);

      using var ms = new MemoryStream();
      bmp.Save(ms, ImageFormat.Png);
      var imageData = new ReadOnlyMemory<byte>(ms.ToArray());

      string prompt = mode.ToLower() switch
      {
         "verify" => $"I just performed the following action: \"{input}\". Does the screenshot show that this action was completed successfully? Answer with 'Yes' or 'No' and explain.",
         "locate" => $"Please look at this screenshot and return the screen coordinates (X,Y) of the element described as: \"{input}\". Respond only with the coordinates in the format X,Y (e.g., 150,300), followed by a brief reason.",
         _ => throw new ArgumentException("Invalid mode. Use 'verify' or 'locate'.")
      };

      var items = new ChatMessageContentItemCollection
    {
        new TextContent(prompt),
        new ImageContent(imageData, "image/png")
    };

      var history = new ChatHistory();
      history.AddUserMessage(items);

      Console.ForegroundColor = ConsoleColor.Cyan;
      Console.WriteLine(mode.ToLower() == "verify"
          ? "📸 Verifying action..."
          : "🔍 Searching for element coordinates...");
      Console.ResetColor();

      var result = await chatService.GetChatMessageContentsAsync(history);
      var message = result.FirstOrDefault()?.Content?.Trim() ?? "No response from model.";

      if (mode.ToLower() == "verify")
      {
         Console.ForegroundColor = message.StartsWith("Yes", StringComparison.OrdinalIgnoreCase)
             ? ConsoleColor.Green
             : ConsoleColor.Red;
         Console.WriteLine($"{(message.StartsWith("Yes", StringComparison.OrdinalIgnoreCase) ? "✅ Success" : "❌ Failed")}: {message}");
      }
      else if (mode.ToLower() == "locate")
      {
         Console.ForegroundColor = ConsoleColor.Yellow;
         Console.WriteLine($"📍 Coordinates identified: {message}");

         // Attempt to extract coordinates and draw bounding box
         var coordPart = message.Split('\n')[0].Split(',')[..2];
         if (int.TryParse(coordPart[0].Trim(), out int x) && int.TryParse(coordPart[1].Trim(), out int y))
         {
            using var annotatedBmp = new Bitmap(bmp);
            using var graphics = Graphics.FromImage(annotatedBmp);
            using var pen = new Pen(Color.Red, 3);
            graphics.DrawRectangle(pen, x - 10, y - 10, 20, 20); // 20x20 box around the point

            var outputPath = Path.Combine(Directory.GetCurrentDirectory(), $"located-element-{DateTime.Now:yyyyMMddHHmmss}.png");
            annotatedBmp.Save(outputPath, ImageFormat.Png);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"🖼️ Annotated image saved to: {outputPath}");
         }
         else
         {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("❌ Failed to parse coordinates.");
         }
      }

      Console.ResetColor();
      return message;
   }
}
