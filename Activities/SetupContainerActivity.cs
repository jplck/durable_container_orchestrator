using System;
using System.Threading.Tasks;
using ContainerRunnerFuncApp.Exceptions;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using ContainerRunnerFuncApp.Model;

namespace ContainerRunnerFuncApp.Activities
{
    public static class SetupContainerActivity
    {
        [FunctionName("Container_Setup_Activity")]
        public static async Task<(bool, ContainerInstanceReference)> SetupContainerActivityAsync([ActivityTrigger] IDurableActivityContext ctx, ILogger log)
        {

            var (instanceReference, commandLine) = ctx.GetInput<(ContainerInstanceReference, string)>();

            try
            {
                if ((instanceReference.Created && !await RestartExistingContainer(instanceReference, log)) || !instanceReference.Created) {
                    var containerGroup = await CreateNewContainer(instanceReference, commandLine, log);
                    log.LogInformation("Created new container.");
                    return (true, containerGroup);
                }

                log.LogInformation("Restarted container successfully.");
                return (false, instanceReference);

            }
            catch (Exception ex)
            {
                if (ex is ArgumentNullException) {
                    throw new UnableToRecoverException(ex.Message);
                }

                log.LogError("Unable to create/restart container instance");
                throw new TriggerRetryException("Unable to create/restart container. Try again later.");
            }
        }

        private static async Task<ContainerInstanceReference> CreateNewContainer(ContainerInstanceReference instanceReference, string commandLine, ILogger log)
        {
            var acrHost = Helpers.GetConfig()["ACR_HOST"];
            var acrImageName = Helpers.GetConfig()["ACR_IMG_NAME"];
            var resourceGroupName = Helpers.GetConfig()["ACI_Resource_Group"];

            _ = acrHost ?? throw new ArgumentNullException("ACR Host cannot be null");
            _ = acrImageName ?? throw new ArgumentNullException("ACR Image Name cannot be null");
            _ = resourceGroupName ?? throw new ArgumentNullException("Resource Group name cannot be null");

            log.LogWarning("No previous container instances available. Creating new one...");
            var containerGroup = await ContainerRunnerLib.Instance
                                   .CreateContainerGroupAsync(instanceReference.Name,
                                                              resourceGroupName,
                                                              $"{acrHost}/{acrImageName}",
                                                              commandLine,
                                                              log);
            return containerGroup;
        }

        private static async Task<bool> RestartExistingContainer(ContainerInstanceReference instanceReference, ILogger log)
        {
            log.LogWarning("Restarting available instance.");
            var group = await ContainerRunnerLib.Instance.GetContainerGroupAsync(instanceReference, log);

            if (group == null) {
                return false;
            }

            if (group.State != "Stopped")
            {
                log.LogWarning("Force stopping container instance.");
                await group.StopAsync();
            }
            await ContainerRunnerLib.Instance.StartContainerGroupAsync(instanceReference, log);
            return true;
        }
    }
}
