/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.6.0.0; DO NOT EDIT. */

#nullable enable

namespace JsonComm.dtmi_codegen_communicationTest_jsonModel__1
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using JsonComm;

    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.6.0.0")]
    public class TelemetryCollection
    {
        /// <summary>
        /// The 'lengths' Telemetry.
        /// </summary>
        [JsonPropertyName("lengths")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public List<double>? Lengths { get; set; } = default;

        /// <summary>
        /// The 'proximity' Telemetry.
        /// </summary>
        [JsonPropertyName("proximity")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Enum_Proximity? Proximity { get; set; } = default;

        /// <summary>
        /// The 'schedule' Telemetry.
        /// </summary>
        [JsonPropertyName("schedule")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Object_Schedule? Schedule { get; set; } = default;

    }
}
