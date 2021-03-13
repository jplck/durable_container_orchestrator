using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ContainerRunnerFuncApp.Activities;
using ContainerRunnerFuncApp.Exceptions;
using ContainerRunnerFuncApp.Model;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace ContainerRunnerFuncApp
{
    public class Orchestrator
    {
        private readonly ILogger _log;
        private readonly IConfiguration _configuration;

        public Orchestrator(IConfiguration configuration, ILogger<Orchestrator> log)
        {
            _configuration = configuration;
            _log = log;
        }

        [FunctionName("ACI_Main_Orchestrator_Func")]
        public async Task RunMainOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var events = context.GetInput<List<EventGridEventPayload>>();
            List<Task> orchestrations = new List<Task>();

            var retryOptions = new RetryOptions(TimeSpan.FromSeconds(15), 15)
            {
                BackoffCoefficient = 1.5,
                Handle = (ex) => ex.InnerException.Message == TriggerRetryException.DefaultMessage
            };

            events.ForEach(delegate (EventGridEventPayload eventPayload)
            {
                orchestrations.Add(context.CallSubOrchestratorWithRetryAsync<bool>("ACI_Sub_Orchestrator_Func", retryOptions, eventPayload));
            });

            await Task.WhenAll(orchestrations);
        }

        [FunctionName("ACI_Sub_Orchestrator_Func")]
        public async Task<bool> RunSubOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            ContainerInstanceReference instanceRef = null;
            var entityId = new EntityId("ContainerInstanceStatusEntity", "ContainerInstanceStatusEntity");
            var entity = context.CreateEntityProxy<IContainerInstanceStatusEntity>(entityId);

            try
            {
                var retryOptions = new RetryOptions(TimeSpan.FromSeconds(15), 5)
                {
                    BackoffCoefficient = 1.5,
                    Handle = (ex) => ex.InnerException.Message == TriggerRetryException.DefaultMessage
                };

                _log.LogInformation("Starting new orchestration run...");
                
                var blobPayload = context.GetInput<EventGridEventPayload>();

                var containerGroupPrefix = _configuration["ContainerGroupPrefix"] ?? "aci-container-group";

                var containerGroupName = SdkContext.RandomResourceName($"{containerGroupPrefix}-", 6);

                var instanceCount = 0;
                ContainerInstanceReference instance = null;

                using (await context.LockAsync(entityId))
                {
                    instance = await entity.GetNextAvailableContainerGroupAsync();

                    if (instance == null)
                    {
                        instanceCount = await entity.GetContainerGroupCountAsync();
                        var maxInstances = int.Parse(_configuration["MaxNumberOfInstances"]);

                        if (instanceCount >= maxInstances) 
                        { 
                            _log.LogInformation("Maximum number of parallel container instances reached. Triggering retry.");
                            throw new TriggerRetryException(); 
                        }

                        instance = await entity.ReserveEmptyContainerGroupReference(containerGroupName);
                    }
                }

                bool isNew;
                (isNew, instanceRef) = await context.CallActivityWithRetryAsync<(bool, ContainerInstanceReference)>(
                    "Container_Setup_Activity", 
                    retryOptions, 
                    ( instance, string.Empty )
                );

                if (isNew)
                {
                    using (await context.LockAsync(entityId))
                    {
                        await entity.FillEmptyContainerGroupReference(instanceRef);
                    }
                }

                var externalEventTriggerEventName = _configuration["WorkDoneCallbackKeyword"];

                var response = await context.CallActivityWithRetryAsync<string>(
                    "Container_StartWork_Activity",
                    retryOptions,
                    (
                        context.InstanceId,
                        externalEventTriggerEventName,
                        blobPayload.Data.Url,
                        instanceRef
                    )
                );

                var workDoneEvent = await context.WaitForExternalEvent<ContainerResponse>(externalEventTriggerEventName);

                if (!workDoneEvent.Success) { throw new TriggerRetryException(); }
 
                _log.LogInformation("Work done successfully");

                await context.CallActivityAsync("Container_Stop_Activity", instanceRef);
                await entity.ReleaseContainerInstance(instanceRef);

                return true;
            }
            catch (Exception ex)
            {
                var rethrowEx = ex;
                if (ex is TriggerRetryException)
                {
                    _log.LogWarning("Container was unable to execute tasks. Triggering retry.");
                    throw ex;
                } 

                if (instanceRef != null) {
                    _log.LogWarning("Shutting down container instance due to unrecoverable error.");
                    await context.CallActivityAsync("Container_Stop_Activity", instanceRef);
                    await entity.ReleaseContainerInstance(instanceRef);
                }

                throw rethrowEx;
            }
        }
    }
}