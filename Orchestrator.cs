using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ContainerRunnerFuncApp.Activities;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace ContainerRunnerFuncApp
{
    public static class Orchestrator
    {
        [FunctionName("ACI_Reset_Orchestrator_Func")]
        public static async Task RunResetOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger log)
        {
            var entityId = new EntityId("ContainerInstanceStatusEntity", "ContainerInstanceStatusEntity");
            var entity = context.CreateEntityProxy<IContainerInstanceStatusEntity>(entityId);
            await entity.Reset();
        }

        [FunctionName("ACI_Main_Orchestrator_Func")]
        public static async Task RunMainOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger log)
        {
            var events = context.GetInput<List<string>>();
            List<Task> orchestrations = new List<Task>();

            var retryOptions = new RetryOptions(TimeSpan.FromSeconds(15), 15)
            {
                BackoffCoefficient = 1.5,
                Handle = ex => ex.ToString() == "ContainerInstanceExceedingLimitsException"
            };

            events.ForEach(delegate (string eventPayload)
            {
                orchestrations.Add(context.CallSubOrchestratorWithRetryAsync<bool>("ACI_Sub_Orchestrator_Func", retryOptions, eventPayload));
            });

            await Task.WhenAll(orchestrations);
        }

        [FunctionName("ACI_Sub_Orchestrator_Func")]
        public static async Task<bool> RunSubOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger log)
        {
            ContainerInstanceReference instanceRef = null;
            var entityId = new EntityId("ContainerInstanceStatusEntity", "ContainerInstanceStatusEntity");
            var entity = context.CreateEntityProxy<IContainerInstanceStatusEntity>(entityId);

            try
            {
                var containerGroupPrefix = Helpers.GetConfig()["ContainerGroupPrefix"] ?? "aci-container-group";

                var containerGroupName = SdkContext.RandomResourceName($"{containerGroupPrefix}-", 6);

                var eventPayload = context.GetInput<string>();

                var instanceCount = 0;
                ContainerInstanceReference instance = null;

                using (await context.LockAsync(entityId))
                {
                    instance = await entity.GetNextAvailableContainerGroupAsync();

                    if (instance == null)
                    {
                        instanceCount = await entity.GetContainerGroupCountAsync();
                        var maxInstances = int.Parse(Helpers.GetConfig()["Max_Number_Of_Instances"]);

                        if (instanceCount >= maxInstances) { throw new ContainerInstanceExceedingLimitsException(); }

                        instance = await entity.ReserveEmptyContainerGroupReference(containerGroupName);
                    }
                }

                bool isNew;
                (isNew, instanceRef) = await context.CallActivityAsync<(bool, ContainerInstanceReference)>("Container_Setup_Activity", (instance, string.Empty));
                if (isNew)
                {
                    using (await context.LockAsync(entityId))
                    {
                        await entity.FillEmptyContainerGroupReference(instanceRef);
                    }
                }

                //do work with instance
                var response = await context.CallActivityAsync<string>("Container_StartWork_Activity", instanceRef);

                var workDoneEvent = await context.WaitForExternalEvent<ContainerResponse>("Done");

                if (workDoneEvent.Success)
                {
                    log.LogInformation("Work done successfully");
                } else
                {
                    //trigger a retry.
                }

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
                //If above failed and we have an instance running, force shut it down.
                if (instanceRef != null)
                {
                    log.LogError("Shutting down rogue container instance.");
                    await context.CallActivityAsync("Container_Stop_Activity", instanceRef);
                    await entity.ReleaseContainerInstance(instanceRef);
                }
                throw ex;
            }
        }
    }
}