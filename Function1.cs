using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Management.ContainerInstance.Fluent;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace ContainerRunnerFuncApp
{
    public static class Function1
    {
        private const int MaxNumberOfInstances = 2;

        [FunctionName("ACI_Main_Orchestrator_Func")]
        public static async Task RunMainOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger log)
        {
            var events = context.GetInput<List<string>>();

            var retryOptions = new RetryOptions(TimeSpan.FromSeconds(15), 15)
            {
                BackoffCoefficient = 1.5
            };

            List<Task> orchestrations = new List<Task>();

            try
            {

                events.ForEach(delegate (string eventPayload)
                {
                    orchestrations.Add(context.CallSubOrchestratorWithRetryAsync<bool>("ACI_Sub_Orchestrator_Func", retryOptions, eventPayload));
                });

            }
            catch (Exception ex)
            {
                log.LogWarning("An error occured.");
            }

            await Task.WhenAll(orchestrations);
        }

        [FunctionName("ACI_Sub_Orchestrator_Func")]
        public static async Task<bool> RunSubOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger log)
        {
            try
            {
                var eventPayload = context.GetInput<string>();

                var entityId = new EntityId("ContainerInstanceStatusEntity", "ContainerInstanceStatusEntity");

                var entity = context.CreateEntityProxy<IContainerInstanceStatusEntity>(entityId);

                var instanceCount = 0;
                ContainerInstanceReference instance = null;

                using (await context.LockAsync(entityId))
                {
                    instance = await entity.GetNextAvailableInstanceAsync();

                    if (instance == null)
                    {
                        instanceCount = await entity.GetInstanceCountAsync();

                        if (instanceCount >= MaxNumberOfInstances) { throw new ContainerInstanceExceedingLimitsException(); }

                        await entity.ReserveInstanceCapacity();
                    }
                }

                string startupCommand = "echo \"test\"";
                var instanceRef = await context.CallActivityAsync<ContainerInstanceReference>("Container_Setup_Activity", (instance, startupCommand));
                await entity.AddInstanceIfNotExistsAsync(instanceRef);

                //do work with instance
                /*var response = await context.CallActivityAsync<string>("Container_DoWork_Activity", instance);
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine(response);
                Console.ForegroundColor = ConsoleColor.White;*/

                await context.CallActivityAsync("Container_Stop_Activity", instanceRef);
                await entity.ReleaseContainerInstance(instanceRef);

                return true;
            }
            catch (ContainerInstanceExceedingLimitsException exeedsLimitsException)
            {
                log.LogWarning("Currently exeeding container limits. Automatic retry enabled.");
                throw exeedsLimitsException;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        [FunctionName("Container_Setup_Activity")]
        public static async Task<ContainerInstanceReference> SetupContainerActivityAsync([ActivityTrigger] IDurableActivityContext ctx, ILogger log)
        {

            var (instanceReference, commandLine) = ctx.GetInput<(ContainerInstanceReference, string)>();

            if (instanceReference != null)
            {
                log.LogWarning("Restarting available instance.");
                await ContainerRunnerLib.Instance.StartContainerGroupAsync(instanceReference, log);
            }
            else
            {
                log.LogWarning("No previous container instances available. Creating new one...");
                var containerGroup = await ContainerRunnerLib.Instance
                                       .CreateContainerGroupAsync("aci-demo-rg",
                                                                  "aci-demo",
                                                                  "mcr.microsoft.com/azuredocs/aci-helloworld",
                                                                  commandLine,
                                                                  log);
                return containerGroup;
            }

            return instanceReference;
        }

        [FunctionName("Container_Stop_Activity")]
        public static async Task StopContainerActivityAsync([ActivityTrigger] ContainerInstanceReference containerInstance, ILogger log)
        {
            await ContainerRunnerLib.Instance.StopContainerGroupAsync(containerInstance, log);
        }

        [FunctionName("Container_DoWork_Activity")]
        public static async Task<string> WorkContainerActivityAsync([ActivityTrigger] ContainerInstanceReference containerInstance, ILogger log)
        {
            return await ContainerRunnerLib.Instance.SendRequestToContainerInstance(containerInstance, "/api", null, log);
        }

        [FunctionName("Function1_HttpStart")]
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