// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Connector
{
    public class MqttConnectionConfiguration
    {
        [JsonPropertyName("host")]
        public required string Host { get; set; }

        [JsonPropertyName("KeepAliveSeconds")]
        public required int KeepAliveSeconds { get; set; }

        [JsonPropertyName("maxInflightMessages")]
        public required int MaxInflightMessages { get; set; }

        [JsonPropertyName("protocol")]
        public required string Protocol { get; set; }

        [JsonPropertyName("sessionExpirySeconds")]
        public required int SessionExpirySeconds { get; set; }

        [JsonPropertyName("authentication")]
        public required MqttConnectionConfigurationAuthentication Authentication { get; set; }

        [JsonPropertyName("tls")]
        public required MqttConnectionConfigurationTls Tls { get; set; }
    }
}
