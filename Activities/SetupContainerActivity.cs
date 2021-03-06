using System;
using System.Threading.Tasks;
using ContainerRunnerFuncApp.Exceptions;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using ContainerRunnerFuncApp.Model;
using Microsoft.Extensions.Configuration;

namespace ContainerRunnerFuncApp.Activities
{
    public class SetupContainerActivity
    {
        private readonly IConfiguration _config;
        private readonly ILogger _log;

        private readonly ContainerRunnerLib _containerRunner;

        public SetupContainerActivity(ILogger<SetupContainerActivity> logger, IConfiguration configuration, ContainerRunnerLib containerRunner)
        {
            _log = logger;
            _config = configuration;
            _containerRunner = containerRunner;
        }

        [FunctionName("Container_Setup_Activity")]
        public async Task<(bool, ContainerInstanceReference)> SetupContainerActivityAsync([ActivityTrigger] IDurableActivityContext ctx)
        {

            var (instanceReference, commandLine) = ctx.GetInput<(ContainerInstanceReference, string)>();

            try
            {
                if (instanceReference.Created) {
                    await _containerRunner.StartContainerGroupAsync(instanceReference, _log);
                    _log.LogInformation($"Restarted {instanceReference.InstanceId} with Ip: {instanceReference.IpAddress}");
                    return (false, instanceReference);
                }

                var containerGroup = await CreateNewContainer(instanceReference, commandLine);
                _log.LogInformation("Created new container.");
                return (true, containerGroup);
            }
            catch (Exception ex)
            {
                if (ex is ArgumentNullException) {
                    throw new UnableToRecoverException(ex.Message);
                }

                _log.LogError($"Unable to create/restart container instance {instanceReference.InstanceId}: {ex.Message}");
                throw new TriggerRetryException();
            }
        }

        private async Task<ContainerInstanceReference> CreateNewContainer(
            ContainerInstanceReference instanceReference,
            string commandLine
        )
        {
            var acrHost = _config["ACRHost"];
            var acrImageName = _config["ACRImgName"];
            var resourceGroupName = _config["ACIResourceGroup"];

            _ = acrHost ?? throw new ArgumentNullException("ACR Host cannot be null");
            _ = acrImageName ?? throw new ArgumentNullException("ACR Image Name cannot be null");
            _ = resourceGroupName ?? throw new ArgumentNullException("Resource Group name cannot be null");

            _log.LogWarning("No previous container instances available. Creating new one...");
            var containerGroup = await _containerRunner.CreateContainerGroupAsync(instanceReference.Name,
                                                                                  resourceGroupName,
                                                                                  $"{acrHost}/{acrImageName}",
                                                                                  commandLine,
                                                                                  _log);
            return containerGroup;
        }
    }
}
