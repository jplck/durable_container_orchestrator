using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using System;

namespace ContainerRunnerFuncApp
{
    public static class OrchestrationTrigger
    {
        [FunctionName("Orchestration_Trigger_Func_HTTP")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {

            var events = new List<string>
            {
                "e1",
                "e2",
                "e3",
                "e4",
                "e5"
            };

            string instanceId = await starter.StartNewAsync("ACI_Main_Orchestrator_Func", events);
            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
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
