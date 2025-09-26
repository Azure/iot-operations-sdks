namespace Azure.Iot.Operations.TDParser
{
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
            }
        };

        public static TDThing? Parse(string tdJson)
        {
            return JsonSerializer.Deserialize<TDThing>(tdJson, serializerOptions);
        }
    }
}
