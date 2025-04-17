// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Azure.Iot.Operations.Connector.ConnectorConfigurations
{
    internal class MqttConnectionConfiguration
    {
        [JsonPropertyName("host")]
        public required string Host { get; set; }

        [JsonPropertyName("keepAliveSeconds")]
        public int? KeepAliveSeconds { get; set; }

        [JsonPropertyName("maxInflightMessages")]
        public ushort? MaxInflightMessages { get; set; }

        [JsonPropertyName("protocol")]
        public required string Protocol { get; set; }

        [JsonPropertyName("sessionExpirySeconds")]
        public int? SessionExpirySeconds { get; set; }

        [JsonPropertyName("authentication")]
        public MqttConnectionConfigurationAuthentication? Authentication { get; set; }

        [JsonPropertyName("tls")]
        public MqttConnectionConfigurationTls? Tls { get; set; }
    }
}
