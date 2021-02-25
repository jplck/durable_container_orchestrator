using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using System;
using Newtonsoft.Json;
using Microsoft.Azure.EventGrid.Models;

namespace ContainerRunnerFuncApp
{
    public class EventGridEventPayload : EventGridEvent
    {
        [JsonProperty("data")]
        public new EventGridBlobEventData Data { get; set; }
    }

    public class EventGridBlobEventData
    {
        [JsonProperty("api")]
        public string Api { get; set; }

        [JsonProperty("clientRequestId")]
        public string ClientRequestId { get; set; }

        [JsonProperty("requestId")]
        public string RequestId { get; set; }

        [JsonProperty("eTag")]
        public string ETag { get; set; }

        [JsonProperty("contentType")]
        public string ContentType { get; set; }

        [JsonProperty("contentLength")]
        public string ContentLength { get; set; }

        [JsonProperty("blobType")]
        public string BlobType { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }
    }

    public static class OrchestrationTrigger
    {
        [FunctionName("Orchestration_Trigger_Func_ServiceBus")]
        public static async Task RunVerifierStartServiceBusQueueTrigger(
                    [ServiceBusTrigger("%SbQueueName%", Connection = "ServiceBusConnection")] string eventData,
                    [DurableClient] IDurableOrchestrationClient starter,
                    ILogger log)
        {
            var eventPayload = JsonConvert.DeserializeObject<EventGridEventPayload>(eventData);

            if (eventPayload.EventType == @"Microsoft.Storage.BlobCreated")
            {
                string instanceId = await starter.StartNewAsync("ACI_Main_Orchestrator_Func", new List<EventGridEventPayload> { eventPayload });
                log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
            }

        }

        [FunctionName("Orchestration_Reset_Trigger_Func_HTTP")]
        public static async Task<IActionResult> TriggerResetRun(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "reset/{instanceId}")] HttpRequestMessage req,
            [DurableClient] IDurableEntityClient entityClient, [DurableClient] IDurableOrchestrationClient durableClient,
            string instanceId,
            ILogger log)
        {
            try
            {
                log.LogInformation($"Stop orchestrator with instanceId: {instanceId}");
                await durableClient.TerminateAsync(instanceId, "Due to reset");

                log.LogInformation("Start entity reset.");
                var entityId = new EntityId("ContainerInstanceStatusEntity", "ContainerInstanceStatusEntity");
                await entityClient.SignalEntityAsync(entityId, "Reset");
            }
            catch (Exception)
            {
                return new BadRequestResult();
            }
            return new AcceptedResult();
        }
    }
}
