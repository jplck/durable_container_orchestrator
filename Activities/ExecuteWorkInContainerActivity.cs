using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System.Net.WebSockets;
using System;
using System.Threading;
using System.Net;
using Newtonsoft.Json;

namespace ContainerRunnerFuncApp.Activities
{
    public class ContainerRequest
    {
        [JsonProperty("blobUri")]
        public string BlobUri { get; set; }
    }

    public class ContainerResponse
    {
        [JsonProperty("blobUri")]
        public string BlobUri { get; set; }

        [JsonProperty("success")]
        public bool Success { get; set; }
    }

    public static class ExecuteWorkInContainerActivity
    {
        [FunctionName("Container_StartWork_Activity")]
        public static async Task<string> StartWorkContainerActivityAsync([ActivityTrigger] ContainerInstanceReference containerInstance, ILogger log)
        {
            log.LogWarning($"Doing some work on instance {containerInstance.Name}.");
            var result = await ContainerRunnerLib.Instance.SendRequestToContainerInstance(containerInstance, "/api", JsonConvert.SerializeObject(new ContainerRequest
            {
                BlobUri = ""
            }), log);

            return result;
        }
    }
}
