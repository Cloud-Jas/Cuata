using Microsoft.Extensions.Configuration;

namespace Cuata.Services
{
   public class ScreenshotClient
   {
      private readonly HttpClient _httpClient;
      private readonly IConfiguration _configuration;

      public ScreenshotClient(HttpClient httpClient, IConfiguration configuration)
      {
         _httpClient = httpClient;
         _configuration = configuration;
      }

      public async Task<bool> SendScreenshotAsync(byte[] imageData, string meetingId)
      {
         var functionUrl = _configuration["Azure:ScreenshotFunctionUrl"];
         if (string.IsNullOrWhiteSpace(functionUrl))
         {
            Console.WriteLine("❌ Azure Function URL is missing.");
            return false;
         }

         using var content = new MultipartFormDataContent
    {
        { new StringContent(meetingId), "meetingId" },
        { new ByteArrayContent(imageData), "screenshot", $"screenshot-{DateTime.UtcNow:yyyyMMddHHmmss}.png" }
    };

         var response = await _httpClient.PostAsync(functionUrl, content);
         if (response.IsSuccessStatusCode)
         {
            Console.WriteLine("✅ Screenshot sent successfully.");
            return true;
         }

         Console.WriteLine($"⚠️ Failed to send screenshot. Status: {response.StatusCode}");
         return false;
      }

   }

}
