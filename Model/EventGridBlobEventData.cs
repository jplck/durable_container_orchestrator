using Microsoft.Azure.EventGrid.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ContainerRunnerFuncApp.Model
{
    public class EventGridEventPayload : EventGridEvent
    {
        [JsonProperty("data")]
        public new EventGridBlobEventData Data { get; set; }
    }

    public class EventGridBlobEventData
    {
        [JsonProperty("api")]
        public string Api { get; set; }

        [JsonProperty("clientRequestId")]
        public string ClientRequestId { get; set; }

        [JsonProperty("requestId")]
        public string RequestId { get; set; }

        [JsonProperty("eTag")]
        public string ETag { get; set; }

        [JsonProperty("contentType")]
        public string ContentType { get; set; }

        [JsonProperty("contentLength")]
        public string ContentLength { get; set; }

        [JsonProperty("blobType")]
        public string BlobType { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }
    }
}
