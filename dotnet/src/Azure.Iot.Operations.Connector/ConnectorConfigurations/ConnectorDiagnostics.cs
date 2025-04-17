// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Azure.Iot.Operations.Connector.ConnectorConfigurations
{
    internal class ConnectorDiagnostics
    {
        [JsonPropertyName("logs")]
        public required ConnectorDiagnosticsLogs Logs { get; set; }
    }
}
