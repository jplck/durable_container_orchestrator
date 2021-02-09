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
    }

    [JsonObject(MemberSerialization.OptIn)]
    class ContainerInstanceStatusEntity : IContainerInstanceStatusEntity
    {
        [JsonProperty("instances")]
        private List<ContainerInstanceReference> Instances { get; set; }

        [JsonProperty("reservedInstanceCounter")]
        private int ReservedInstanceCounter { get; set; }

        public async Task Reset()
        {
            Instances.Clear();
            ReservedInstanceCounter = 0;
        }

        public async Task AddInstanceIfNotExistsAsync(ContainerInstanceReference instance)
        {
            if (Instances == null) { Instances = new List<ContainerInstanceReference>(); }
            if (Instances.Any(i => i.Name == instance.Name)) { return; }
            ReservedInstanceCounter -= 1;
            Instances.Add(instance);
        }

        public async Task ReleaseContainerInstance(ContainerInstanceReference instance)
        {
            var foundInstance = Instances?.Find((i) => i.InstanceId == instance.InstanceId);
            foundInstance.Available = true;
        }

        public async Task ReserveInstanceCapacity() => ReservedInstanceCounter += 1;

        public Task<int> GetInstanceCountAsync() => Task.FromResult((Instances?.Count ?? 0) + ReservedInstanceCounter);

        public Task<List<ContainerInstanceReference>> GetInstancesAsync() =>  Task.FromResult(Instances ?? new List<ContainerInstanceReference>());

        public Task<ContainerInstanceReference> GetNextAvailableInstanceAsync()
        {
            var instance = Instances?.Find((instance) => instance.Available);
            if (instance != null) { instance.Available = false; }
            return Task.FromResult(instance);
        }

        [FunctionName(nameof(ContainerInstanceStatusEntity))]
        public static Task Run([EntityTrigger] IDurableEntityContext ctx)
        => ctx.DispatchAsync<ContainerInstanceStatusEntity>();
    }

    public interface IContainerInstanceStatusEntity
    {
        public Task<ContainerInstanceReference> GetNextAvailableInstanceAsync();

        public Task AddInstanceIfNotExistsAsync(ContainerInstanceReference instance);

        public Task<List<ContainerInstanceReference>> GetInstancesAsync();

        public Task ReleaseContainerInstance(ContainerInstanceReference instance);

        public Task<int> GetInstanceCountAsync();

        public Task ReserveInstanceCapacity();

        public Task Reset();
    }
}
