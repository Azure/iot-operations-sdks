// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Azure.Iot.Operations.Connector
{
    public class AioDiagnosticsLogs
    {
        [JsonPropertyName("level")]
        public required string Level { get; set; }
    }
}
