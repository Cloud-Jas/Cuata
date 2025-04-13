using Cuata.MeetingsIngestor.Models;
using Microsoft.Azure.Cosmos;

namespace Cuata.MeetingsIngestor.Services;

public class CosmosDbService
{
   private readonly Container _container;

   public CosmosDbService(CosmosClient client, string dbName, string containerName)
   {
      _container = client.GetContainer(dbName, containerName);
   }

   public async Task UpsertMeetingAsync(Meeting meeting)
   {
      try
      {
         var response = await _container.ReadItemAsync<Meeting>(meeting.id, new PartitionKey(meeting.userId));
         var existing = response.Resource;
         if (existing.startTime != meeting.startTime)
         {
            meeting.notified = false;
         }
         if (!meeting.cancelled && existing.cancelled)
         {
            meeting.cancelled = true;
         }
         meeting.notified = meeting.notified || existing.notified;
      }
      catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
      {
      }

      await _container.UpsertItemAsync(meeting, new PartitionKey(meeting.userId));
   }
   public async Task<List<Meeting>> GetMeetingsForTodayAsync(string userId)
   {
      var today = DateTime.UtcNow.Date;
      var tomorrow = today.AddDays(1);

      var query = new QueryDefinition(
          "SELECT * FROM c WHERE c.userId = @userId AND c.cancelled = false AND c.startTime >= @today AND c.startTime < @tomorrow")
          .WithParameter("@userId", userId)
          .WithParameter("@today", today)
          .WithParameter("@tomorrow", tomorrow);

      var meetings = new List<Meeting>();
      using var iterator = _container.GetItemQueryIterator<Meeting>(query, requestOptions: new QueryRequestOptions
      {
         PartitionKey = new PartitionKey(userId)
      });

      while (iterator.HasMoreResults)
      {
         var response = await iterator.ReadNextAsync();
         meetings.AddRange(response);
      }

      return meetings;
   }


   public async Task<List<Meeting>> GetUpcomingMeetingsAsync()
   {
      var now = DateTime.UtcNow;
      var inTenMins = now.AddMinutes(10);

      var query = new QueryDefinition(
          "SELECT * FROM c WHERE c.startTime >= @now AND c.startTime <= @in10 AND c.notified = false AND NOT IS_NULL(c.teamsLink)"
      )
      .WithParameter("@now", now)
      .WithParameter("@in10", inTenMins);

      var result = new List<Meeting>();
      var iterator = _container.GetItemQueryIterator<Meeting>(query);

      while (iterator.HasMoreResults)
      {
         var response = await iterator.ReadNextAsync();
         result.AddRange(response);
      }

      return result;
   }

   public async Task MarkAsNotifiedAsync(Meeting meeting)
   {
      meeting.notified = true;
      await _container.ReplaceItemAsync(meeting, meeting.id, new PartitionKey(meeting.userId));
   }
}
