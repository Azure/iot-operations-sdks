namespace Azure.Iot.Operations.ProtocolCompiler.IntegrationTests.TestCaseGenerator
{
    using System.Linq;
    using System.Text.Json;

    public class StringishSchemaInstantiator : IPrimitiveSchemaInstantiator
    {
        private readonly string[] values;
        private int valueIx;

        public StringishSchemaInstantiator(JsonElement configElt)
        {
            values = configElt.EnumerateArray().Select(e => e.GetString()!).ToArray();
            valueIx = 0;
        }

        public void InstantiateSchema(Utf8JsonWriter jsonWriter)
        {
            string value = values[valueIx];
            valueIx = (valueIx + 1) % values.Length;
            jsonWriter.WriteStringValue(value);
        }
    }
}
