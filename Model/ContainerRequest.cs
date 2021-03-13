using Newtonsoft.Json;

namespace ContainerRunnerFuncApp
{
    public class ContainerRequest
    {
        [JsonProperty("blobUri")]
        public string BlobUri { get; set; }

        [JsonProperty("externalTriggerCallbackUrl")]
        public string ExternalTriggerCallbackUrl { get; set; }
    }
}