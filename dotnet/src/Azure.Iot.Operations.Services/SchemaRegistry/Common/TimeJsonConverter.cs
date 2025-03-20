// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

namespace Azure.Iot.Operations.Services.SchemaRegistry
{
    using System;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Class for customized JSON conversion of <c>TimeOnly</c> values to/from string representations in ISO 8601 Time format.
    /// </summary>
    internal sealed class TimeJsonConverter : JsonConverter<TimeOnly>
    {
        /// <inheritdoc/>
        public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return TimeOnly.FromDateTime(DateTime.Parse(reader.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.NoCurrentDateDefault | DateTimeStyles.AdjustToUniversal));
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString("o", CultureInfo.InvariantCulture) + "Z");
        }
    }
}
