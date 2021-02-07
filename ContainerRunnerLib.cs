using Microsoft.Azure.Management.ContainerInstance.Fluent;
using Microsoft.Azure.Management.ContainerInstance.Fluent.Models;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace ContainerRunnerFuncApp
{
    class ContainerInstanceExceedingLimitsException : Exception
    {
        public ContainerInstanceExceedingLimitsException() { }
    }

    class ContainerInstanceCommandExecutionFailedException : Exception
    {
        public ContainerInstanceCommandExecutionFailedException() { }
    }

    class ContainerRunnerLib
    {
        private static IAzure _azure;

        private static readonly Lazy<ContainerRunnerLib> lazy = new Lazy<ContainerRunnerLib>(() => new ContainerRunnerLib());

        private ContainerRunnerLib()
        {
            _azure = Azure.Authenticate("./credentials.json").WithDefaultSubscription();
        }

        public static ContainerRunnerLib Instance => lazy.Value;

        public async Task<ContainerInstanceReference> CreateContainerGroupAsync(string resourceGroupName, string containerGroupPrefix, string imageName, string startupCommand, ILogger log)
        {
            IResourceGroup resourceGroup = null;
            try
            {
                resourceGroup = await _azure.ResourceGroups.GetByNameAsync(resourceGroupName);
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
                        //.WithStartingCommandLine(startupCommand)
                        .Attach()
                    .WithDnsPrefix(containerGroupName)
                    .WithRestartPolicy(ContainerGroupRestartPolicy.Never)
                    .CreateAsync();
            
                Console.WriteLine($"Container group with container Id {containerGroup.Id} created.");

                return new ContainerInstanceReference()
                {
                    ResourceGroupName = resourceGroup.Name,
                    Name = containerGroupName,
                    Available = false,
                    Fqdn = containerGroup.Fqdn,
                    InstanceId = containerGroup.Id,
                    StartupCommand = startupCommand,
                    Ports = new List<int>()
                    {
                        80
                    }
                };
            }
            catch (Exception ex)
            {
                log.LogError(ex.Message);
                throw ex;
            }
        }
       
        public async Task<string> SendRequestToContainerInstance(ContainerInstanceReference containerInstance, string path, string content, ILogger log)
        {
            var url = $"https://{containerInstance.Fqdn}{path}";

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var method = new HttpMethod("GET");
                    var request = new HttpRequestMessage(method, url);

                    if (content != null && content != string.Empty)
                    {
                        method = new HttpMethod("POST");

                        request = new HttpRequestMessage(method, url)
                        {
                            Content = new StringContent(content, Encoding.UTF8, "application/json")
                        };

                    }

                    using (HttpResponseMessage response = client.SendAsync(request).Result)
                    {
                        response.EnsureSuccessStatusCode();
                        return await response.Content.ReadAsStringAsync();
                    }
                }
            }
            catch (Exception)
            {
                throw new ContainerInstanceCommandExecutionFailedException();
            }
        }

        public async Task DeleteContainerGroupAsync(ContainerInstanceReference containerInstance, ILogger log)
        {
            await _azure.ContainerGroups.DeleteByIdAsync(containerInstance.InstanceId);
        }

        public async Task StartContainerGroupAsync(ContainerInstanceReference containerInstance, ILogger log)
        {
            log.LogInformation("(Re)Starting container instance from exsiting registration...");

            await _azure.ContainerGroups.StartAsync(containerInstance.ResourceGroupName, containerInstance.Name);

            log.LogInformation("Container instance made available.");
        }

        public async Task StopContainerGroupAsync(ContainerInstanceReference containerInstance, ILogger log)
        {
            var aci = await _azure.ContainerGroups.GetByIdAsync(containerInstance.InstanceId);
            await aci.StopAsync();
        }

        public async Task<IContainerExecResponse> ExecuteContainerCommand(ContainerInstanceReference containerInstance, string command, ILogger log)
        {
            try
            {
                var aci = await _azure.ContainerGroups.GetByIdAsync(containerInstance.InstanceId);
                return await aci.ExecuteCommandAsync(containerInstance.Name, command, 0, 0);
            }
            catch (Exception)
            {
                throw new ContainerInstanceCommandExecutionFailedException();
            }
        }
    }
}
