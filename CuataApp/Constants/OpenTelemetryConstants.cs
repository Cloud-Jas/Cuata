using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cuata.Constants
{
   public class OpenTelemetryConstants
   {
      // CONSTANTS FOR MESSAGE BROKER OTEL STRINGS 
      public const string MESSAGING_SYSTEM_KEY = "messaging.system";
      public const string MESSAGING_SYSTEM_VALUE = "kafka";
      public const string MESSAGING_DESTINATION_KEY = "messaging.destination";
      public const string MESSAGING_DESTINATION_KIND_KEY = "messaging.destination.kind";
      public const string MESSAGING_DESTINATION_KIND_VALUE = "topic";
      public const string MESSAGING_PROTOCOL_KEY = "messaging.protocol";
      public const string MESSAGING_PROTOCOL_VALUE = "kafka";
      public const string MESSAGING_URL_KEY = "messaging.url";
      public const string MESSAGING_ID_KEY = "messaging.message_id";

      // CONSTANTS FOR DATABASE OTEL STRINGS
      public const string DATABASE_SYSTEM_KEY = "db.system";
      public const string DATABASE_SYSTEM_VALUE = "cosmosdb";
      public const string NET_PEER_NAME_KEY = "net.peer.name";
      public const string NET_PEER_PORT_KEY = "net.peer.port";
      public const string NET_PEER_PORT_VALUE = "443";
      public const string DATABASE_NAME_KEY = "db.name";
      public const string DATABASE_OPERATION_KEY = "db.operation";
      public const string DATABASE_COLLECTION_NAME_KEY = $"db.{DATABASE_SYSTEM_VALUE}.collection";


      // CONSTANTS FOR OTEL GENERAL ATTRIBUTES
      public const string TRACEID_KEY = "traceId";
      public const string PARENT_SPANID_KEY = "parentSpanId";
      public const string PARENT_SPAN_TRACEFLAG_KEY = "parentSpanTraceFlag";
      public const string OPERATION_STARTTIME_KEY = "StartTime";
      public const string OPERATION_ENDTIME_KEY = "EndTime";

      // CONSTANTS FOR AZURE AI INFERENCE OTEL STRINGS
      public const string GEN_AI_SYSTEM_KEY = "gen_ai.system";
      public const string GEN_AI_SYSTEM_VALUE = "az.ai.inference";
      public const string GEN_AI_OPERATION_NAME_KEY = "gen_ai.operation.name";
      public const string GEN_AI_REQUEST_MODEL_KEY = "gen_ai.request.model";
      public const string GEN_AI_REQUEST_MAX_TOKENS_KEY = "gen_ai.request.max_tokens";
      public const string GEN_AI_REQUEST_TEMPERATURE_KEY = "gen_ai.request.temperature";
      public const string GEN_AI_REQUEST_TOP_P_KEY = "gen_ai.request.top_p";
      public const string GEN_AI_REQUEST_TOP_K_KEY = "gen_ai.request.top_k";
      public const string GEN_AI_REQUEST_STOP_SEQUENCES_KEY = "gen_ai.request.stop_sequences";
      public const string GEN_AI_RESPONSE_MODEL_KEY = "gen_ai.response.model";
      public const string GEN_AI_RESPONSE_ID_KEY = "gen_ai.response.id";
      public const string GEN_AI_RESPONSE_FINISH_REASONS_KEY = "gen_ai.response.finish_reasons";
      public const string GEN_AI_USAGE_INPUT_TOKENS_KEY = "gen_ai.usage.input_tokens";
      public const string GEN_AI_USAGE_OUTPUT_TOKENS_KEY = "gen_ai.usage.output_tokens";
      public const string AZ_NAMESPACE_KEY = "az.namespace";
      public const string AZ_NAMESPACE_VALUE = "Microsoft.CognitiveServices";
      public const string SERVER_ADDRESS_KEY = "server.address";
      public const string SERVER_PORT_KEY = "server.port";
      public const string GEN_AI_USER_MESSAGE = "gen_ai.user.message";
      public const string GEN_AI_ASSISTANT_MESSAGE = "gen_ai.assistant.message";
      public const string GEN_AI_TOOL_MESSAGE= "gen_ai.tool.message";
      public const string GEN_AI_TOOL_NAME = "gen_ai.tool.name";
      public const string GEN_AI_ERROR_TYPE = "error.type";
      public const string GEN_AI_AGENT_NAME = "gen_ai.agent.name";
      public const string GEN_AI_AGENT_ID = "gen_ai.agent.id";
      public const string GEN_AI_AGENT_DESCRIPTION = "gen_ai.agent.description";


   }
}
