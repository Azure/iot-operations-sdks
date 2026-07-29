// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector
{
    public partial class ConnectorRuntimeHealth
    {
        /// <summary>
        /// A human-readable message describing the last transition.
        /// </summary>
        public string? Message { get; set; } = default;

        /// <summary>
        /// Unique, CamelCase reason code describing the cause of the last health state transition.
        /// </summary>
        public string? ReasonCode { get; set; } = default;

        /// <summary>
        /// The current health status of the resource.
        /// </summary>
        public HealthStatus Status { get; set; } = default!;
    }
}
