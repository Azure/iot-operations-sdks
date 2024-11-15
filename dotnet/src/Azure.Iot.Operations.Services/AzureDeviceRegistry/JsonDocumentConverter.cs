﻿using System.Text.Json.Serialization;
using System.Text.Json;

namespace Azure.Iot.Operations.Services.Assets
{
    internal class JsonDocumentConverter : JsonConverter<JsonDocument>
    {
        public override JsonDocument Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            JsonDocumentOptions jsonOptions = new()
            {
                AllowTrailingCommas = options.AllowTrailingCommas,
                MaxDepth = options.MaxDepth,
            };

            return JsonDocument.Parse(reader.GetString()!, jsonOptions);
        }

        public override void Write(Utf8JsonWriter writer, JsonDocument dateTimeValue, JsonSerializerOptions options) =>
               throw new NotImplementedException(); // This library should never need to serialize this type
    }
}
