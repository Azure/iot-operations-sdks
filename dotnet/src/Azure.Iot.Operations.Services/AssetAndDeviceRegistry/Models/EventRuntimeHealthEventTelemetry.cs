namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;

    public partial class EventRuntimeHealthEventTelemetry
    {
        /// <summary>
        /// Telemetry event emitted for reporting the runtime health of events.
        /// </summary>
        public EventRuntimeHealthEventSchema EventRuntimeHealthEvent { get; set; } = default!;
    }
}
