using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace ContainerRunnerFuncApp
{
    class ContainerInstanceStatusEntityException : Exception
    {
        public ContainerInstanceStatusEntityException() { }
    }

    public class ContainerInstanceReference
    {
        [JsonProperty("instanceId")]
        public string InstanceId { get; set; }

        [JsonProperty("available")]
        [DefaultValue(false)]
        public bool Available { get; set; }

        [JsonProperty("fqdn")]
        public string Fqdn { get; set; }

        [JsonProperty("ports")]
        public List<int> Ports { get; set; }

        [JsonProperty("resourceGroupName")]
        public string ResourceGroupName { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("startupCommand")]
        public string StartupCommand { get; set; }

        [JsonProperty("created")]
        [DefaultValue(false)]
        public bool Created { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    class ContainerInstanceStatusEntity : IContainerInstanceStatusEntity
    {
        [JsonProperty("instances")]
        private List<ContainerInstanceReference> Instances { get; set; } = new List<ContainerInstanceReference>();

        public async Task Reset()
        {
            Instances.Clear();
        }

        public async Task ReleaseContainerInstance(ContainerInstanceReference containerInstance)
        {
            _ = containerInstance ?? throw new ArgumentNullException("Container instance reference cannot be null.");
            var foundInstance = GetExistingInstanceByName(containerInstance.Name);
            foundInstance.Available = true;
        }

        public async Task<ContainerInstanceReference> ReserveEmptyContainerGroupReference(string name)
        {
            var instance = new ContainerInstanceReference()
            {
                Name = name
            };

            Instances.Add(instance);
            return instance;
        }

        public Task<int> GetContainerGroupCountAsync() => Task.FromResult(Instances?.Count ?? 0);

        public Task<List<ContainerInstanceReference>> GetInstancesAsync() =>  Task.FromResult(Instances);

        public Task<ContainerInstanceReference> GetNextAvailableContainerGroupAsync()
        {
            var instance = Instances?.Find((instance) => instance.Available);
            if (instance != null) { instance.Available = false; }
            return Task.FromResult(instance);
        }

        [FunctionName(nameof(ContainerInstanceStatusEntity))]
        public static Task Run([EntityTrigger] IDurableEntityContext ctx)
        => ctx.DispatchAsync<ContainerInstanceStatusEntity>();

        public async Task FillEmptyContainerGroupReference(ContainerInstanceReference containerReference)
        {
            var idx = Instances.FindIndex(instance => instance.Name == containerReference.Name);
            Instances[idx] = containerReference;
        }

        private ContainerInstanceReference GetExistingInstanceByName(string instanceName) => Instances?.Find((i) => i.Name == instanceName);
    }

    public interface IContainerInstanceStatusEntity
    {
        public Task<ContainerInstanceReference> GetNextAvailableContainerGroupAsync();

        public Task<List<ContainerInstanceReference>> GetInstancesAsync();

        public Task ReleaseContainerInstance(ContainerInstanceReference instance);

        public Task<int> GetContainerGroupCountAsync();

        public Task<ContainerInstanceReference> ReserveEmptyContainerGroupReference(string name);

        public Task Reset();

        public Task FillEmptyContainerGroupReference(ContainerInstanceReference containerReference);
    }
}
