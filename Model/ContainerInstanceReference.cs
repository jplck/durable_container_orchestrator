using Newtonsoft.Json;
using System.ComponentModel;
using System.Collections.Generic;

namespace ContainerRunnerFuncApp.Model
{
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
        public int ExternalPort { get; set; }

        [JsonProperty("resourceGroupName")]
        public string ResourceGroupName { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("startupCommand")]
        public string StartupCommand { get; set; }

        [JsonProperty("created")]
        [DefaultValue(false)]
        public bool Created { get; set; }

        [JsonProperty("ipAddress")]
        public string IpAddress { get; set; }
    }
}