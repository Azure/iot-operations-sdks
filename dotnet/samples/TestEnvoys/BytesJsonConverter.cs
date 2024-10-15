/* Code generated by Azure.Iot.Operations.ProtocolCompiler; DO NOT EDIT. */

namespace TestEnvoys
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Class for customized JSON conversion of <c>byte[]</c> values to/from Base64 string representations per RFC 4648.
    /// </summary>
    internal sealed class BytesJsonConverter : JsonConverter<byte[]>
    {
        /// <inheritdoc/>
        public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return Convert.FromBase64String(reader.GetString()!);
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(Convert.ToBase64String(value));
        }
    }
}
