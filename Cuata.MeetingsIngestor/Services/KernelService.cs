using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;

#pragma warning disable SKEXP0110 
namespace Cuata.MeetingsIngestor.Services
{
   public class KernelService : IKernelService
   {
      private readonly IChatCompletionService _chatCompletionService;

      public KernelService(IChatCompletionService chatCompletionService)
      {
         _chatCompletionService = chatCompletionService;
      }

      public async Task<ChatMessageContent> GetChatMessageContentAsync(Kernel kernel, string prompt, OpenAIPromptExecutionSettings? promptExecutionSettings)
      {
         try
         {
            if (string.IsNullOrWhiteSpace(prompt))
            {
               throw new ArgumentException("Prompt cannot be null or empty.", nameof(prompt));
            }

            OpenAIPromptExecutionSettings settings = new OpenAIPromptExecutionSettings();

            settings = new()
            {
               ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
               TopP = 1,
               Temperature = 0.7
            };

            if (promptExecutionSettings != null)
               settings = promptExecutionSettings;

            return await _chatCompletionService.GetChatMessageContentAsync(
                prompt,
                executionSettings: settings,
                kernel: kernel
            );
         }
         catch (Exception ex)
         {
            throw new Exception("Error in GetChatMessageContentAsync", ex);
         }
      }
   }
}