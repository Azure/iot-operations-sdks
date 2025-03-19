/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.9.0.0; DO NOT EDIT. */

#nullable enable

namespace CloudEvents.Oven
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using CloudEvents;

    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.9.0.0")]
    public partial class OperationSummarySchema
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
