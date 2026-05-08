namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public partial class ConfigStatus
    {
        /// <summary>
        /// The error that this entity encountered, if any error was encountered.
        /// </summary>
        /// <remarks>
        /// If a device/endpoint/asset/dataset/etc has no errors to report, this value should be null.
        /// </remarks>
        public ConfigError? Error { get; set; } = default;

        /// <summary>
        /// A read only timestamp indicating the last time the configuration has been modified from the perspective of the current actual (Edge) state of the CRD. Edge would be the only writer of this value and would sync back up to the cloud.
        /// </summary>
        public DateTime? LastTransitionTime { get; set; } = default;

        /// <summary>
        /// A read only incremental counter indicating the number of times the configuration has been modified from the perspective of the current actual (Edge) state of the CRD. Edge would be the only writer of this value and would sync back up to the cloud. In steady state, this should equal version.
        /// </summary>
        public ulong? Version { get; set; } = default;

        internal bool EqualTo(ConfigStatus other)
        {
            if (Error == null && other.Error != null)
            {
                return false;
            }
            else if (Error != null && other.Error == null)
            {
                return false;
            }
            else if (Error != null && other.Error != null && !Error.EqualTo(other.Error))
            {
                return false;
            }

            if (Version != other.Version)
            {
                return false;
            }

            // deliberately ignore the lastUpdateTimeUtc field since this comparison is used to check
            // if a new status update should be sent and a new status update should not be sent if the only
            // change was lastUpdateTimeUtc

            return true;
        }
    }
}
