/* Code generated by Azure.Iot.Operations.ProtocolCompiler; DO NOT EDIT. */

#nullable enable

namespace TestEnvoys.dtmi_com_example_Counter__1
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using TestEnvoys;

    public class IncrementResponsePayload
    {
        /// <summary>
        /// The Command response argument.
        /// </summary>
        [JsonPropertyName("CounterResponse")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public int CounterResponse { get; set; } = default!;

    }
}
