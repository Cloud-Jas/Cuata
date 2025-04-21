using Cuata;
using Cuata.Services;
using System.Drawing;
using System.Drawing.Imaging;

public class ScreenshotService
{
   private readonly ScreenshotClient _screenshotClient;
   private CancellationTokenSource? _cts;
   public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

   public ScreenshotService(ScreenshotClient screenshotClient)
   {
      _screenshotClient = screenshotClient;
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

   private async Task LoopScreenshotAsync(string meetingId, CancellationToken token)
   {
      while (!token.IsCancellationRequested)
      {
         var image = CaptureAndCompressScreenshot();
         await SendToAzureFunctionAsync(meetingId, image);
         await Task.Delay(TimeSpan.FromSeconds(20), token);
      }
   }

   private async Task SendToAzureFunctionAsync(string meetingId, byte[] imageData)
   {
      await _screenshotClient.SendScreenshotAsync(imageData, meetingId);
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
