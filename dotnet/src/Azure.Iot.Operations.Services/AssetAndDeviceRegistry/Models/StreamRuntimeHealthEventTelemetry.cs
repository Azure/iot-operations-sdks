namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;

    public partial class StreamRuntimeHealthEventTelemetry
    {
        /// <summary>
        /// Telemetry event emitted for reporting the runtime health of streams.
        /// </summary>
        public StreamRuntimeHealthEventSchema StreamRuntimeHealthEvent { get; set; } = default!;
    }
}
