namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;

    public partial class ManagementActionRuntimeHealthEventTelemetry
    {
        /// <summary>
        /// Telemetry event emitted for reporting the runtime health of management actions.
        /// </summary>
        public ManagementActionRuntimeHealthEventSchema ManagementActionRuntimeHealthEvent { get; set; } = default!;
    }
}
