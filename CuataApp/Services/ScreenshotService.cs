using Azure.Storage.Blobs;
using Cuata;
using Cuata.Services;
using Microsoft.Extensions.Configuration;
using System.Drawing;
using System.Drawing.Imaging;

public class ScreenshotService
{
   private readonly ScreenshotClient _screenshotClient;
   private CancellationTokenSource? _cts;
   public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;
   private readonly IConfiguration _configuration;

   public ScreenshotService(ScreenshotClient screenshotClient,IConfiguration configuration)
   {
      _screenshotClient = screenshotClient;
      _configuration = configuration;
   }


   public void Start(string meetingTitle,string meetingId)
   {
      _cts = new CancellationTokenSource();
      Task.Run(() => LoopScreenshotAsync(meetingId, _cts.Token));
   }

   public void Stop()
   {
      _cts?.Cancel();
   }

   public async Task ConsolidateSummaryAsync()
   {
      await _screenshotClient.ConsolidateSummaryAsync();
   }

   private async Task LoopScreenshotAsync(string meetingId, CancellationToken token)
   {
      while (!token.IsCancellationRequested)
      {
         await Task.Delay(TimeSpan.FromSeconds(20), token);
         var image = CaptureAndCompressScreenshot();
         var blobUri = await SaveScreenshotToBlob(image);
         await SendToAzureFunctionAsync(meetingId, blobUri);
         await Task.Delay(TimeSpan.FromSeconds(20), token);
      }
   }
   public async Task<string> SaveScreenshotToBlob(byte[] imageBytes)
   {
      var conn = _configuration.GetValue<string>("AzureWebJobsStorage")!;
      var blobClient = new BlobContainerClient(conn, "screenshots");
      await blobClient.CreateIfNotExistsAsync();

      var blobName = $"screenshot-{DateTime.UtcNow:yyyyMMddHHmmssfff}.png";
      var blob = blobClient.GetBlobClient(blobName);
      await blob.UploadAsync(new MemoryStream(imageBytes), overwrite: true);

      return blob.Uri.ToString();
   }
   private async Task SendToAzureFunctionAsync(string meetingId, string blobUri)
   {
      await _screenshotClient.SendScreenshotAsync(meetingId, blobUri);
   }

   public byte[] CaptureAndCompressScreenshot()
   {
      var sc = new ScreenCapture();
      using var img = sc.CaptureScreen();
      int screenWidth = img.Width;
      int screenHeight = img.Height;

      using var bmp = new Bitmap(img);
      using var ms = new MemoryStream();


      ImageCodecInfo jpegCodec = ImageCodecInfo.GetImageEncoders()
          .First(codec => codec.FormatID == ImageFormat.Jpeg.Guid);


      var encoderParams = new EncoderParameters(1);
      encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 85L); // High-quality JPEG (85%)

      bmp.Save(ms, jpegCodec, encoderParams);

      return ms.ToArray();
   }

}
