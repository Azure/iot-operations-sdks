﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Azure.Iot.Operations.Connector
{
    public class ConnectorDiagnosticsLogs
    {
        [JsonPropertyName("level")]
        public required string Level { get; set; }
    }
}
