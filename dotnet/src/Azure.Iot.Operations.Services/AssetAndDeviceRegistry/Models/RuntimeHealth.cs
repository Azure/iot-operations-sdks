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

        internal static void CompareNewHealthWithCachedHealth(RuntimeHealth newHealth, RuntimeHealth? cachedHealth, out bool updateCache, out bool sendIt)
        {
            if (cachedHealth == null)
            {
                // This is the first health status event (or the first status event since the user paused reporting), so report it and start periodically reporting
                updateCache = true;
                sendIt = true;
                return;
            }

            if (RuntimeHealth.Equals(newHealth, cachedHealth))
            {
                // The reported health status is no different than the last reported status, so do nothing. This last reported status
                // will be sent by the background reporting later if it doesn't change prior to the next period.
                updateCache = false;
                sendIt = false;
                return;
            }

            if (newHealth.Version < cachedHealth.Version)
            {
                // The reported health status belongs to an older version, so it should not be reported or cached
                updateCache = false;
                sendIt = false;
                return;
            }

            if (RuntimeHealth.EqualsExceptTimestamp(newHealth, cachedHealth) && newHealth.LastUpdateTime.CompareTo(cachedHealth.LastUpdateTime) >= 0)
            {
                // The new health status is identical to the previously sent status, but with a newer timestamp. Just update the timestamp
                // of the cached version so that it is sent on the next background report.
                updateCache = true;
                sendIt = false;
                return;
            }

            // The reported health status is different enough from the last sent status that it should actually be sent to the service and the periodic reporting timer should be restarted
            updateCache = true;
            sendIt = true;
        }

        public static bool Equals(RuntimeHealth? a, RuntimeHealth? b)
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
