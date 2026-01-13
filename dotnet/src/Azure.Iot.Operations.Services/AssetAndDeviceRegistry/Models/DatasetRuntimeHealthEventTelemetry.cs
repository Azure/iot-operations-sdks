namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;

    public partial class DatasetRuntimeHealthEventTelemetry
    {
        /// <summary>
        /// Telemetry event emitted for reporting the runtime health of datasets.
        /// </summary>
        public DatasetRuntimeHealthEventSchema DatasetRuntimeHealthEvent { get; set; } = default!;
    }
}
