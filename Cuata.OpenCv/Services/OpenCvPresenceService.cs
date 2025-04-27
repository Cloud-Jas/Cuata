using Microsoft.Extensions.Hosting;
using OpenCvSharp;

namespace Cuata.OpenCv.Services;
public class OpenCvPresenceService : BackgroundService
{
   private readonly ServiceBusPublisherService serviceBusPublisherService;
   public OpenCvPresenceService(ServiceBusPublisherService serviceBusPublisherService)
   {
      this.serviceBusPublisherService = serviceBusPublisherService;
   }
   protected override async Task ExecuteAsync(CancellationToken stoppingToken)
   {
      try
      {
         Console.WriteLine("🟢 OpenCV presence detection started");
         var capture = new VideoCapture(0);
         var faceCascade = new CascadeClassifier("Models/haarcascade_frontalface_default.xml");
         var previousPresenceState = false;
         int i = 0;
         while (true)
         {
            using var frame = new Mat();
            capture.Read(frame);
            if (frame.Empty())
            {
               await Task.Delay(100, stoppingToken);
               continue;
            }

            var gray = new Mat();
            Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
            var faces = faceCascade.DetectMultiScale(gray, 1.1, 4);
            foreach (var face in faces)
            {
               Cv2.Rectangle(frame, face, Scalar.Green, 2);
            }

            if (faces.Length > 0)
            {
               if (i == 0)
               {
                  previousPresenceState = true;
                  await serviceBusPublisherService.SendMessageAsync("Present");
                  Console.WriteLine("Message sent to Service Bus: You are present");
               }
               if (!previousPresenceState && i > 0)
               {
                  previousPresenceState = true;
                  i = 0;
                  await serviceBusPublisherService.SendMessageAsync("Present");
                  Console.WriteLine("Message sent to Service Bus: You are present");
               }
               Console.WriteLine("🧑 You are present");
               previousPresenceState = true;
            }
            else
            {
               if (i == 0)
               {
                  previousPresenceState = false;
                  await serviceBusPublisherService.SendMessageAsync("Left");
                  Console.WriteLine("Message sent to Service Bus: You are NOT present");
               }
               if (previousPresenceState && i > 0)
               {
                  previousPresenceState = false;
                  i = 0;
                  await serviceBusPublisherService.SendMessageAsync("Left");
                  Console.WriteLine("Message sent to Service Bus: You are NOT present");
               }
               Console.WriteLine("🚫 You are NOT present");
               previousPresenceState = false;
            }

            i++;

            Thread.Sleep(10000);
         }
      }
      catch (Exception ex)
      {
         Console.WriteLine($"❌ Error: {ex.Message}");
      }
      finally
      {
         Console.WriteLine("🔴 OpenCV presence detection stopped");
      }

   }
}
