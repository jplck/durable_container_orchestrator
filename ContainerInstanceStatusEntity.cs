using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using ContainerRunnerFuncApp.Model;
using System.Threading.Tasks;

namespace ContainerRunnerFuncApp
{
    [JsonObject(MemberSerialization.OptIn)]
    class ContainerInstanceStatusEntity : IContainerInstanceStatusEntity
    {
        [JsonProperty("instances")]
        private List<ContainerInstanceReference> Instances { get; set; } = new List<ContainerInstanceReference>();

        #pragma warning disable CS1998
        public async Task Reset()
        {
            Instances.Clear();
        }

        #pragma warning disable CS1998
        public async Task ReleaseContainerInstance(ContainerInstanceReference containerInstance)
        {
            _ = containerInstance ?? throw new ArgumentNullException("Container instance reference cannot be null.");
            var foundInstance = GetExistingInstanceByName(containerInstance.Name);
            foundInstance.Available = true;
        }

        #pragma warning disable CS1998
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

        #pragma warning disable CS1998
        public async Task FillEmptyContainerGroupReference(ContainerInstanceReference containerReference)
        {
            var idx = Instances.FindIndex(instance => instance.Name == containerReference.Name);
            Instances[idx] = containerReference;
        }

        private ContainerInstanceReference GetExistingInstanceByName(string instanceName) => Instances?.Find((i) => i.Name == instanceName);
    }

    public interface IContainerInstanceStatusEntity
    {
        Task<ContainerInstanceReference> GetNextAvailableContainerGroupAsync();

        Task<List<ContainerInstanceReference>> GetInstancesAsync();

        Task ReleaseContainerInstance(ContainerInstanceReference instance);

        Task<int> GetContainerGroupCountAsync();

        Task<ContainerInstanceReference> ReserveEmptyContainerGroupReference(string name);

        Task Reset();

        Task FillEmptyContainerGroupReference(ContainerInstanceReference containerReference);
    }
}
