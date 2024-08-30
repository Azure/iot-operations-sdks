/* This is an auto-generated file.  Do not modify. */

#nullable enable

namespace Azure.Iot.Operations.Services.SchemaRegistry.dtmi_ms_adr_SchemaRegistry__1
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public class PutCommandRequest
    {
        /// <summary>
        /// The Command request argument.
        /// </summary>
        [JsonPropertyName("putSchemaRequest")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public Object_Put_Request PutSchemaRequest { get; set; } = default!;

    }
}
