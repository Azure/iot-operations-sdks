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

        internal static bool Equals(RuntimeHealth? a, RuntimeHealth? b)
        {
            // if one is null and the other is not, then they are not equal
            if ((a == null) != (b == null))
            {
                return false;
            }

            if ((a == null) && (b == null))
            {
                return true;
            }

            return EqualsExceptTimestamp(a, b) && (a?.LastUpdateTime.CompareTo(b?.LastUpdateTime) == 0);
        }

        internal static bool EqualsExceptTimestamp(RuntimeHealth? a, RuntimeHealth? b)
        {
            // if one is null and the other is not, then they are not equal
            if ((a == null) != (b == null))
            {
                return false;
            }

            if ((a == null) && (b == null))
            {
                return true;
            }

            return string.Equals(a?.Message, b?.Message) && string.Equals(a?.ReasonCode, b?.ReasonCode) && (a?.Status == b?.Status) && ulong.Equals(a?.Version, b?.Version);
        }
    }
}
