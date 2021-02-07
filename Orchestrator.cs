using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace ContainerRunnerFuncApp
{
    public static class Orchestrator
    {
        [FunctionName("ACI_Main_Orchestrator_Func")]
        public static async Task RunMainOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger log)
        {
            var events = context.GetInput<List<string>>();
            List<Task> orchestrations = new List<Task>();

            var retryOptions = new RetryOptions(TimeSpan.FromSeconds(15), 15)
            {
                BackoffCoefficient = 1.5
            };

            try
            {

                events.ForEach(delegate (string eventPayload)
                {
                    orchestrations.Add(context.CallSubOrchestratorWithRetryAsync<bool>("ACI_Sub_Orchestrator_Func", retryOptions, eventPayload));
                });

            }
            catch (Exception)
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
                        var maxInstances = int.Parse(Helpers.GetConfig()["Max_Number_Of_Instances"]);

                        if (instanceCount >= maxInstances) { throw new ContainerInstanceExceedingLimitsException(); }

                        await entity.ReserveInstanceCapacity();
                    }
                }

                string startupCommand = "echo \"test\"";

                //Startup command only applied to newly created container instances. The existing instances will run the startup command on restart.
                //Custom command need to be executed in the next steps.

                var instanceRef = await context.CallActivityAsync<ContainerInstanceReference>("Container_Setup_Activity", (instance, startupCommand));
                await entity.AddInstanceIfNotExistsAsync(instanceRef);

                //do work with instance
                var response = await context.CallActivityAsync<string>("Container_DoWork_Activity", instanceRef);

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
    }
}