using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System.Collections.Generic;

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
    }
}
