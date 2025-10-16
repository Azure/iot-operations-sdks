namespace Azure.Iot.Operations.TDParser
{
    using System.Collections.Generic;
    using System.Text;
    using System.Text.Json;
    using Azure.Iot.Operations.TDParser.Model;

    public class TDParser
    {
        private static JsonSerializerOptions serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters =
            {
                new ContextSpecifierJsonConverter(),
                new AdditionalPropSpecifierJsonConverter(),
                new TDStringArrayJsonConverter(),
            }
        };

        public static TDThing? Parse(string tdJson)
        {
            return JsonSerializer.Deserialize<TDThing>(tdJson, serializerOptions);
        }

        public static List<TDThing> ParseMultiple(string tdJson)
        {
            Utf8JsonReader reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(tdJson));
            JsonElement rootElt = JsonElement.ParseValue(ref reader);

            if (rootElt.ValueKind == JsonValueKind.Array)
            {
                return JsonSerializer.Deserialize<List<TDThing>>(rootElt, serializerOptions) ?? new();
            }
            else if (rootElt.ValueKind == JsonValueKind.Object)
            {
                TDThing? thing = JsonSerializer.Deserialize<TDThing>(rootElt, serializerOptions);
                if (thing != null)
                {
                    return new List<TDThing> { thing };
                }
            }

            return new();
        }
    }
}
