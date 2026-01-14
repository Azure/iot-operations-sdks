namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;

    public partial class RuntimeHealth
    {
        /// <summary>
        /// The timestamp (RFC3339) when the health status was last updated, even if the status did not change.
        /// </summary>
        public DateTime LastUpdateTime { get; set; } = default!;

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

        /// <summary>
        /// The version of the resource for which the runtime health is being reported.
        /// </summary>
        public ulong Version { get; set; } = default!;
    }
}
