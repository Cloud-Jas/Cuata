using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Cuata.Services;

namespace Cuata.Extensions
{
   public static class SemanticKernelExtensions
   {
      public static IServiceCollection AddSemanticKernel(this IServiceCollection services, IConfiguration configuration)
      {
         var credentialOptions = new DefaultAzureCredentialOptions
         {
            TenantId = configuration.GetValue<string>("TenantId")
         };

         services.AddScoped<Kernel>(provider =>
         {
            var builder = Kernel.CreateBuilder();

            builder.AddAzureOpenAIChatCompletion(deploymentName: configuration!.GetValue<string>("OpenAIChatCompletionDeploymentName")!,
                 credentials: new DefaultAzureCredential(credentialOptions),
                 endpoint: configuration!.GetValue<string>("OpenAIEndpoint")!);
            return builder.Build();
         });

         services.AddSingleton<IChatCompletionService, AzureOpenAIChatCompletionService>(provider =>
         {
            return new AzureOpenAIChatCompletionService(
                 deploymentName: configuration!.GetValue<string>("OpenAIChatCompletionDeploymentName")!,
                 credentials: new DefaultAzureCredential(credentialOptions),
                 endpoint: configuration!.GetValue<string>("OpenAIEndpoint")!
             );
         });

         services.AddSingleton<IKernelService, KernelService>();

         return services;
      }
   }
}