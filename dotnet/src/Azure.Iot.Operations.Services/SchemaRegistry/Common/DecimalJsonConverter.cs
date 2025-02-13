// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.8.0.0; DO NOT EDIT. */

namespace Azure.Iot.Operations.Services.SchemaRegistry
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.SchemaRegistry;

    /// <summary>
    /// Class for customized JSON conversion of <c>DecimalString</c> values to/from strings.
    /// </summary>
    internal sealed class DecimalJsonConverter : JsonConverter<DecimalString>
    {
        /// <inheritdoc/>
        public override DecimalString Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return new DecimalString(reader.GetString()!);
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, DecimalString value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
