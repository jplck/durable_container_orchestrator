using Microsoft.Azure.Management.ContainerInstance.Fluent.Models;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ContainerRunnerFuncApp
{
    class ContainerRunnerLib
    {
        private static IAzure _azure;

        private static readonly Lazy<ContainerRunnerLib> lazy = new Lazy<ContainerRunnerLib>(() => new ContainerRunnerLib());

        private ContainerRunnerLib()
        {
            _azure = Azure.Authenticate("./credentials.json").WithDefaultSubscription();
        }

        public static ContainerRunnerLib Instance => lazy.Value;

        public async Task<string> CreateContainerGroupAsync(string resourceGroupName, string containerGroupPrefix, string imageName, ILogger log)
        {
            IResourceGroup resourceGroup = null;
            try
            {
                resourceGroup = await _azure.ResourceGroups.GetByNameAsync(resourceGroupName);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Found resource group with name {resourceGroupName}");
            } catch (Exception)
            {
                log.LogWarning("Resource group does not exist. Trying to create it in next step.");
            }

            try
            {
                resourceGroup ??= await _azure.ResourceGroups.Define(resourceGroupName)
                                    .WithRegion(Region.EuropeWest)
                                    .CreateAsync();

                Console.WriteLine($"Resource group {resourceGroupName} created.");

                var containerGroupName = SdkContext.RandomResourceName($"{containerGroupPrefix}-", 6);

                var acrHost = Helpers.GetConfig()["ACR_Host"];
                var acrUsername = Helpers.GetConfig()["ACR_Username"];
                var acrPwd = Helpers.GetConfig()["ACR_Pwd"];

                var containerGroup = await _azure.ContainerGroups.Define(containerGroupName)
                    .WithRegion(resourceGroup.Region)
                    .WithExistingResourceGroup(resourceGroupName)
                    .WithLinux()
                    //.WithPrivateImageRegistry(acrHost, acrUsername, acrPwd)
                    .WithPublicImageRegistryOnly()
                    .WithoutVolume()
                    .DefineContainerInstance($"{containerGroupName}")
                        .WithImage(imageName)
                        .WithExternalTcpPort(80)
                        .WithCpuCoreCount(1.0)
                        .WithMemorySizeInGB(1.0)
                        .WithEnvironmentVariable("TestVar", "test")
                        .Attach()
                    .WithDnsPrefix(containerGroupName)
                    .WithRestartPolicy(ContainerGroupRestartPolicy.Never)
                    .CreateAsync();

                Console.WriteLine($"Container group with container Id {containerGroup.Id} created.");

                return containerGroup.Id;
            }
            catch (Exception ex)
            {
                log.LogError(ex.Message);
            }

            return null;
        }

        public async Task DeleteContainerGroupAsync(string id, ILogger log)
        {
            await _azure.ContainerGroups.DeleteByIdAsync(id);
        }
    }
}
