// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Azure.Iot.Operations.Connector.ConnectorConfigurations
{
    internal class AioMetadata
    {
        [JsonPropertyName("aioMinVersion")]
        public required string AioMinVersion { get; set; }

        [JsonPropertyName("aioMaxVersion")]
        public required string AioMaxVersion { get; set; }
    }
}
