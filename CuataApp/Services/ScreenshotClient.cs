using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Text;

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


      public async Task ConsolidateSummaryAsync()
      {
         var functionUrl = _configuration["AzureConsolidateSummaryFunctionUrl"];
         if (string.IsNullOrWhiteSpace(functionUrl))
         {
            Console.WriteLine("❌ Azure Function URL is missing.");
            return;
         }

         var meetingId = CuataState.Instance.MeetingId;
         if (string.IsNullOrWhiteSpace(meetingId))
         {
            Console.WriteLine("❌ Meeting ID is missing.");
            return;
         }

         var payload = new Dictionary<string, string>
    {
        { "meetingId", meetingId }
    };

         var json = JsonConvert.SerializeObject(payload);
         using var httpClient = new HttpClient();
         var content = new StringContent(json, Encoding.UTF8, "application/json");

         try
         {
            var response = await httpClient.PostAsync(functionUrl, content);
            if (response.IsSuccessStatusCode)
            {
               Console.WriteLine("✅ Consolidation triggered successfully.");
            }
            else
            {
               Console.WriteLine($"⚠️ Failed to trigger consolidation. Status: {response.StatusCode}");
            }
         }
         catch (Exception ex)
         {
            Console.WriteLine($"🚨 Exception while calling function: {ex.Message}");
         }
      }

      public async Task<bool> SendScreenshotAsync(string meetingId, string blobUri)
      {
         var functionUrl = _configuration["AzureScreenshotFunctionUrl"];
         if (string.IsNullOrWhiteSpace(functionUrl))
         {
            Console.WriteLine("❌ Azure Function URL is missing.");
            return false;
         }

         var payload = new
         {
            meetingId,
            blobUri
         };

         var json = JsonConvert.SerializeObject(payload);
         using var content = new StringContent(json, Encoding.UTF8, "application/json");

         var response = await _httpClient.PostAsync(functionUrl, content);
         if (response.IsSuccessStatusCode)
         {
            Console.WriteLine("✅ Screenshot info sent successfully.");
            return true;
         }

         Console.WriteLine($"⚠️ Failed to send screenshot info. Status: {response.StatusCode}");
         return false;
      }


   }

}
