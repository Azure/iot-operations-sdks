/* This is an auto-generated file.  Do not modify. */

#nullable enable

namespace SampleCloudEvents.dtmi_akri_samples_oven__1
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public class Object_OperationSummary
    {
        /// <summary>
        /// The 'numberOfCakes' Field.
        /// </summary>
        [JsonPropertyName("numberOfCakes")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long? NumberOfCakes { get; set; } = default;

        /// <summary>
        /// The 'startingTime' Field.
        /// </summary>
        [JsonPropertyName("startingTime")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public DateTime? StartingTime { get; set; } = default;

        /// <summary>
        /// The 'totalDuration' Field.
        /// </summary>
        [JsonPropertyName("totalDuration")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public TimeSpan? TotalDuration { get; set; } = default;

    }
}
