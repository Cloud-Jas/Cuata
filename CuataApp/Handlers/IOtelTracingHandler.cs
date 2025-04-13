using Cuata.Models;

namespace Cuata.Handlers
{
   public interface IOtelTracingHandler
   {
      Task<TResponse> TraceRequest<TResponse>(Func<RequestData, Task<TResponse>> runTraceRequest, RequestData requestData);
   }
}