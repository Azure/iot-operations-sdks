/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

#nullable enable

namespace TestEnvoys.Counter
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using TestEnvoys;

    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.10.0.0")]
    public partial class IncrementResponsePayload
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
