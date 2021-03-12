using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System;
using ContainerRunnerFuncApp.Model;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;

namespace ContainerRunnerFuncApp.Activities
{
    public class ContainerRequest
    {
        [JsonProperty("blobUri")]
        public string BlobUri { get; set; }

        [JsonProperty("externalTriggerCallbackUrl")]
        public string ExternalTriggerCallbackUrl { get; set; }
    }

    public class ContainerResponse
    {
        [JsonProperty("blobUri")]
        public string BlobUri { get; set; }

        [JsonProperty("success")]
        public bool Success { get; set; }
    }

    public class ExecuteWorkInContainerActivity
    {
        private readonly IConfiguration _config;
        private readonly ILogger _log;

        private readonly ContainerRunnerLib _containerRunner;

        public ExecuteWorkInContainerActivity(
            ILogger<ExecuteWorkInContainerActivity> log,
            IConfiguration configuration, 
            ContainerRunnerLib containerRunner
        )
        {
            _config = configuration;
            _log = log;
            _containerRunner = containerRunner;
        }

        [FunctionName("Container_StartWork_Activity")]
        public async Task<string> StartWorkContainerActivityAsync([ActivityTrigger] (string, string, string, ContainerInstanceReference) input)
        {
            var (instanceId, externalEventTriggerKeyword, blobUri, containerInstance) = input;

            var host = _config["Host"];
            var functionKey = _config["FunctionKey"];
            var path = _config["ACI_Container_Endpoint_Path"];

            _ = host ?? throw new ArgumentNullException("Host cannot be null");
            _ = path ?? throw new ArgumentNullException("ACI Path cannot be null");

            var functionKeyString = string.IsNullOrEmpty(functionKey) ? string.Empty : $"?code={functionKey}";

            _log.LogWarning($"Doing some work on instance {containerInstance.Name}.");

            var result = await _containerRunner.SendRequestToContainerInstance(
                containerInstance, 
                path, 
                JsonConvert.SerializeObject(new ContainerRequest
                {
                    BlobUri = blobUri,
                    ExternalTriggerCallbackUrl = $"{host}/runtime/webhooks/durabletask/instances/{instanceId}/raiseEvent/{externalEventTriggerKeyword}{functionKeyString}"
                }
            ) , _log);

            return result;
        }
    }
}
