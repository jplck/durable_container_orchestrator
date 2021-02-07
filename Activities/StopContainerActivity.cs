using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace ContainerRunnerFuncApp.Activities
{
    public static class StopContainerActivity
    {
        [FunctionName("Container_Stop_Activity")]
        public static async Task StopContainerActivityAsync([ActivityTrigger] ContainerInstanceReference containerInstance, ILogger log)
        {
            await ContainerRunnerLib.Instance.StopContainerGroupAsync(containerInstance, log);
        }
    }
}
