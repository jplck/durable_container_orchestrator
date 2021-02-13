using System;
using System.Threading.Tasks;
using ContainerRunnerFuncApp.Exceptions;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

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

                if (instanceReference.Created)
                {
                    log.LogWarning("Restarting available instance.");
                    var group = await ContainerRunnerLib.Instance.GetContainerGroupAsync(instanceReference, log);
                    if (group.State != "Stopped")
                    {
                        log.LogWarning("Force stopping container instance.");
                        await group.StopAsync();
                    }
                    await ContainerRunnerLib.Instance.StartContainerGroupAsync(instanceReference, log);
                }
                else
                {
                    log.LogWarning("No previous container instances available. Creating new one...");
                    var containerGroup = await ContainerRunnerLib.Instance
                                           .CreateContainerGroupAsync(instanceReference.Name,
                                                                      "aci-demo-rg",
                                                                      "acrdemo123456.azurecr.io/demo-image:0.1",
                                                                      commandLine,
                                                                      log);
                    return (true, containerGroup);
                }

                return (false, instanceReference);

            }
            catch (Exception ex)
            {
                log.LogError("Unable to create/restart container instance");
                throw new ContainerCreateException("Unable to create/restart container. Try again later.");
            }
        }
    }
}
