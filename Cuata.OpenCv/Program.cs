using Cuata.OpenCv.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text;

namespace Cuata.OpenCv;
class Program
{
   static async Task Main(string[] args)
   {
      Console.OutputEncoding = Encoding.UTF8;
      Console.Title = "👩‍💻 CUATA OpenCV";

      var host = Host.CreateDefaultBuilder(args)
           .ConfigureAppConfiguration((hostingContext, config) =>
           {
              config.AddJsonFile("appsettings.json", optional: true);
              config.AddJsonFile("appsettings.Development.json", optional: false);
           })
           .ConfigureServices((context, services) =>
           {
              services.AddSingleton<ServiceBusPublisherService>();
              services.AddHostedService<OpenCvPresenceService>();

           })
           .Build();

      await host.RunAsync();
   }
}
