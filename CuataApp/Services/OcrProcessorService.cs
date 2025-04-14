using Azure;
using Cuata.Modules;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Microsoft.Extensions.Configuration;
using System.Drawing;
using System.Drawing.Imaging;

namespace Cuata.Services;

public class OcrProcessorService
{
   private readonly IConfiguration _configuration;
   private readonly string endpoint;
   private readonly string key;
   private readonly ComputerVisionClient client;

   public OcrProcessorService(IConfiguration configuration)
   {
      _configuration = configuration;

      endpoint = _configuration["CognitiveServicesVisionEndpoint"];

      key = _configuration["CognitiveServicesVisionKey"];

      client = new ComputerVisionClient(new ApiKeyServiceClientCredentials(key))
      {
         Endpoint = endpoint
      };

   }
   public async Task<List<TextElement>> ExtractTextElementsAsync(string imagePath)
   {
      try
      {
         using var imageStream = File.OpenRead(imagePath);
         var result = await client.ReadInStreamAsync(imageStream);

         const int numberOfCharsInOperationId = 36;
         string operationId = result.OperationLocation.Substring(result.OperationLocation.Length - numberOfCharsInOperationId);


         var operation = await client.GetReadResultAsync(Guid.Parse(operationId));
         while (operation.Status == OperationStatusCodes.Running ||
                operation.Status == OperationStatusCodes.NotStarted)
         {
            await Task.Delay(500);
            operation = await client.GetReadResultAsync(Guid.Parse(operationId));
         }

         var output = new List<TextElement>();

         foreach (var page in operation.AnalyzeResult.ReadResults)
         {
            foreach (var line in page.Lines)
            {
               var points = new List<PointF>();
               for (int i = 0; i < line.BoundingBox.Count; i += 2)
               {
                  points.Add(new PointF((float)line.BoundingBox[i], (float)line.BoundingBox[i + 1]));
               }

               output.Add(new TextElement
               {
                  Text = line.Text,
                  BoundingBox = points
               });
            }
         }

         return output;
      }
      catch (Exception ex)
      {
         throw new Exception($"Error extracting text elements: {ex.Message}", ex);
      }
   }

   public int GetTextElement(List<TextElement> elements, string searchText, string imagePath, bool verbose = false)
   {
      try
      {
         int? foundIndex = null;
         string ocrDir = "ocr";

         if (verbose && !Directory.Exists(ocrDir))
            Directory.CreateDirectory(ocrDir);

         using var image = Image.FromFile(imagePath);
         using var graphics = Graphics.FromImage(image);
         var bluePen = new Pen(Color.Blue, 2);
         var redPen = new Pen(Color.Red, 2);

         for (int i = 0; i < elements.Count; i++)
         {
            var elem = elements[i];
            var points = elem.BoundingBox.ToArray();

            if (verbose)
               graphics.DrawPolygon(bluePen, points);

            if (elem.Text.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            {
               foundIndex = i;
               if (verbose)
                  graphics.DrawPolygon(redPen, points);
            }
         }

         if (foundIndex.HasValue)
         {
            if (verbose)
            {
               string outputPath = Path.Combine(ocrDir, $"ocr_image_{DateTime.Now:yyyyMMdd_HHmmss}.png");
               image.Save(outputPath, ImageFormat.Png);
               Console.WriteLine($"Saved image with bounding boxes to: {outputPath}");
            }

            return foundIndex.Value;
         }
      }
      catch (Exception ex)
      {
         throw new Exception($"Error processing OCR results: {ex.Message}", ex);
      }

      throw new Exception($"Text '{searchText}' not found in OCR results.");
   }

   public Dictionary<string, double> GetTextCoordinates(List<TextElement> elements, int index, string imagePath)
   {
      if (index >= elements.Count)
         throw new IndexOutOfRangeException();

      var box = elements[index].BoundingBox;
      float minX = box.Min(p => p.X);
      float maxX = box.Max(p => p.X);
      float minY = box.Min(p => p.Y);
      float maxY = box.Max(p => p.Y);

      float centerX = (minX + maxX) / 2;
      float centerY = (minY + maxY) / 2;

      using var img = Image.FromFile(imagePath);
      double percentX = Math.Round(centerX / img.Width, 3);
      double percentY = Math.Round(centerY / img.Height, 3);

      return new Dictionary<string, double>
        {
            { "x", percentX },
            { "y", percentY }
        };
   }
}
