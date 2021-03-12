using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using ContainerRunnerFuncApp.Model;

namespace ContainerRunnerFuncApp.Activities
{
    public class StopContainerActivity
    {
        private readonly ILogger _log;
        private readonly ContainerRunnerLib _containerRunner;

        public StopContainerActivity(ContainerRunnerLib containerRunner, ILogger<StopContainerActivity> log)
        {
            _containerRunner = containerRunner;
            _log = log;
        }

        [FunctionName("Container_Stop_Activity")]
        public async Task StopContainerActivityAsync([ActivityTrigger] ContainerInstanceReference containerInstance)
        {
            await _containerRunner.StopContainerGroupAsync(containerInstance, _log);
        }
    }
}
