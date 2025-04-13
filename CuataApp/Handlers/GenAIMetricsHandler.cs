using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cuata.Handlers
{
   public static class GenAIMetricsHandler
   {
      private static readonly Meter ClientMeter = new("GenAI.Client");
      private static readonly Meter ServerMeter = new("GenAI.Server");

      public static readonly Counter<long> TokenUsage = ClientMeter.CreateCounter<long>("gen_ai.client.token.usage");
      public static readonly Histogram<double> ClientOperationDuration = ClientMeter.CreateHistogram<double>("gen_ai.client.operation.duration");

      public static readonly Histogram<double> ServerRequestDuration = ServerMeter.CreateHistogram<double>("gen_ai.server.request.duration");
      public static readonly Histogram<double> TimePerOutputToken = ServerMeter.CreateHistogram<double>("gen_ai.server.time_per_output_token");
      public static readonly Histogram<double> TimeToFirstToken = ServerMeter.CreateHistogram<double>("gen_ai.server.time_to_first_token");
   }
}
