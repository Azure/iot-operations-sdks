namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public partial class ConfigStatus
    {
        /// <summary>
        /// The 'error' Field.
        /// </summary>
        [JsonPropertyName("error")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public ConfigError? Error { get; set; } = default;

        /// <summary>
        /// A read only timestamp indicating the last time the configuration has been modified from the perspective of the current actual (Edge) state of the CRD. Edge would be the only writer of this value and would sync back up to the cloud.
        /// </summary>
        [JsonPropertyName("lastTransitionTime")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public DateTime? LastTransitionTime { get; set; } = default;

        /// <summary>
        /// A read only incremental counter indicating the number of times the configuration has been modified from the perspective of the current actual (Edge) state of the CRD. Edge would be the only writer of this value and would sync back up to the cloud. In steady state, this should equal version.
        /// </summary>
        [JsonPropertyName("version")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public ulong? Version { get; set; } = default;

    }
}
