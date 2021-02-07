using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace ContainerRunnerFuncApp.Activities
{
    public static class ExecuteWorkInContainerActivity
    {
        [FunctionName("Container_DoWork_Activity")]
        public static async Task<string> WorkContainerActivityAsync([ActivityTrigger] ContainerInstanceReference containerInstance, ILogger log)
        {
            log.LogWarning($"Doing some work on instance {containerInstance.Name}.");
            await Task.Delay(15000);
            return string.Empty;
        }
    }
}
