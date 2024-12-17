/* This file will be copied into the folder for generated code. */

namespace Azure.Iot.Operations.Protocol.UnitTests.Serializers.JSON
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Protocol.UnitTests.Serializers.common;

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
