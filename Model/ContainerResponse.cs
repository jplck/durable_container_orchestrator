using Newtonsoft.Json;

namespace ContainerRunnerFuncApp
{
    public class ContainerResponse
    {
        [JsonProperty("blobUri")]
        public string BlobUri { get; set; }

        [JsonProperty("success")]
        public bool Success { get; set; }
    }
}