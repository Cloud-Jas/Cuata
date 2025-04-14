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

public class LocatePlugin
{
   private readonly Kernel _kernel;
   private readonly OcrProcessorService processor;
   public LocatePlugin(Kernel kernel, IServiceProvider serviceProvider)
   {
      _kernel = kernel;
      processor = serviceProvider.GetRequiredService<OcrProcessorService>();
   }

   [KernelFunction, Description("Locate an element in the screenshot based on the Search text")]
   public async Task<string> LocateElementInScreenshot(
    [Description("Search text to be used to locate the element")] string input)
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
      var outputActualPath = Path.Combine(Directory.GetCurrentDirectory(), $"locate-{DateTime.Now:yyyyMMddHHmmss}.png");
      bmp.Save(outputActualPath, ImageFormat.Png);
      var imageData = new ReadOnlyMemory<byte>(ms.ToArray());


      Console.ForegroundColor = ConsoleColor.Yellow;
      Console.WriteLine($"📍 search text: {input}");


      var elements = await processor.ExtractTextElementsAsync(outputActualPath);
      int index = processor.GetTextElement(elements, input, outputActualPath, verbose: true);
      var coordinates = processor.GetTextCoordinates(elements, index, outputActualPath);

      Console.ForegroundColor = ConsoleColor.Cyan;
      Console.WriteLine($"🖼️ Annotated image saved to: {outputActualPath}");
      Console.WriteLine($"📊 Element located at coordinates: {coordinates["x"]}, {coordinates["y"]}");

      int x = (int)((coordinates["x"] * screenWidth));
      int y = (int)((coordinates["y"] * screenHeight));

      Console.WriteLine($"🖱️ Click at coordinates: {x}, {y}");
      Console.ResetColor();

      return $"Click at coordinates: {x}, {y} and the Screen width and height are: {screenWidth}, {screenHeight}";
   }
}
