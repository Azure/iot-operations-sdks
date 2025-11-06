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

        public static ConfigStatus Okay()
        {
            return new()
            {
                Error = null,
                LastTransitionTime = DateTime.UtcNow,
                Version = null, //TODO do we need to report this?
            };
        }
    }
}
