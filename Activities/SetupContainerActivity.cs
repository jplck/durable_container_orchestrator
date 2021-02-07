using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace ContainerRunnerFuncApp.Activities
{
    public static class SetupContainerActivity
    {
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
    }
}
