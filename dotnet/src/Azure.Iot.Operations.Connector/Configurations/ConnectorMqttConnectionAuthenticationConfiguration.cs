// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Connector.ConnectorConfigurations
{
    public class ConnectorMqttConnectionAuthenticationConfiguration
    {
        [JsonPropertyName("method")]
        public required string Method { get; set; }

        [JsonPropertyName("serviceAccountTokenSettings")]
        public required ConnectorMqttConnectionServiceAccountTokenConfigurations ServiceAccountTokenSettings { get; set; }
    }
}
