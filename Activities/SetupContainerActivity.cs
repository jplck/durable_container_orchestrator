using System.Threading.Tasks;
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

            if (instanceReference.Created)
            {
                log.LogWarning("Restarting available instance.");
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
    }
}
