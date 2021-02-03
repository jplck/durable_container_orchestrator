using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace ContainerRunnerFuncApp
{
    public static class Function1
    {
        [FunctionName("ACI_Orchestrator_Func")]
        public static async Task RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var containerGroupId = await context.CallActivityAsync<string>("Container_Create_Activity", null);
            await context.CallActivityAsync("Container_Delete_Activity", containerGroupId);
        }

        [FunctionName("Container_Create_Activity")]
        public static async Task<string> CreateContainerActivityAsync([ActivityTrigger] string name, ILogger log)
        {
            var containerGroupId = await ContainerRunnerLib.Instance
                                       .CreateContainerGroupAsync("aci-demo-rg", "aci-demo", "mcr.microsoft.com/azuredocs/aci-helloworld", log);
            return containerGroupId;
        }

        [FunctionName("Container_Delete_Activity")]
        public static async Task DeleteContainerActivityAsync([ActivityTrigger] string containerGroupId, ILogger log)
        {
            await Task.Delay(15000);
            await ContainerRunnerLib.Instance.DeleteContainerGroupAsync(containerGroupId, log);
        }

        [FunctionName("Function1_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            string instanceId = await starter.StartNewAsync("ACI_Orchestrator_Func", null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}