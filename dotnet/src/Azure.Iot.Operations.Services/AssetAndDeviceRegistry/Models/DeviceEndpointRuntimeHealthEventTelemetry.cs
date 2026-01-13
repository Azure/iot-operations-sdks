namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;

    public partial class DeviceEndpointRuntimeHealthEventTelemetry
    {
        /// <summary>
        /// Telemetry event emitted for reporting the runtime health of the specific inbound endpoint as specified in the topic.
        /// </summary>
        public DeviceEndpointRuntimeHealthEventSchema DeviceEndpointRuntimeHealthEvent { get; set; } = default!;
    }
}
