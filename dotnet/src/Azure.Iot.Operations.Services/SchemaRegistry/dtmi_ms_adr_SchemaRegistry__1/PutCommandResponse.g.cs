/* This is an auto-generated file.  Do not modify. */

#nullable enable

namespace Azure.Iot.Operations.Services.SchemaRegistry.dtmi_ms_adr_SchemaRegistry__1
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public class PutCommandResponse
    {
        /// <summary>
        /// The Command response argument.
        /// </summary>
        [JsonPropertyName("schema")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public Object_Ms_Adr_SchemaRegistry_Schema__1 Schema { get; set; } = default!;

    }
}
