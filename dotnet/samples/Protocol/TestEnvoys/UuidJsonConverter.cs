// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

namespace TestEnvoys
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Class for customized JSON conversion of <c>Guid</c> values to/from UUID string representations per RFC 9562.
    /// </summary>
    internal sealed class UuidJsonConverter : JsonConverter<Guid>
    {
        /// <inheritdoc/>
        public override Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return Guid.ParseExact(reader.GetString()!, "D");
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString("D"));
        }
    }
}
