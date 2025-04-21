using Cuata.MeetingsIngestor.Models;
using Microsoft.Azure.Cosmos;

namespace Cuata.MeetingsIngestor.Services;

public class CosmosMeetingSummaryService
   {
      private readonly Container _container;

      public CosmosMeetingSummaryService(CosmosClient client, string dbName, string containerName)
      {
         _container = client.GetContainer(dbName, containerName);
      }

      public async Task UpsertSummaryAsync(MeetingSummary summary)
      {
         await _container.UpsertItemAsync(summary, new PartitionKey(summary.meetingId));
      }

      public async Task<List<MeetingSummary>> GetSummariesForMeetingAsync(string meetingId)
      {
         var query = new QueryDefinition("SELECT * FROM c WHERE c.meetingId = @meetingId")
             .WithParameter("@meetingId", meetingId);

         var summaries = new List<MeetingSummary>();
         using var iterator = _container.GetItemQueryIterator<MeetingSummary>(query,
             requestOptions: new QueryRequestOptions
             {
                PartitionKey = new PartitionKey(meetingId)
             });

         while (iterator.HasMoreResults)
         {
            var response = await iterator.ReadNextAsync();
            summaries.AddRange(response);
         }

         return summaries;
      }
   }
