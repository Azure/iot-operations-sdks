namespace Azure.Iot.Operations.ProtocolCompiler.IntegrationTests.TestCaseGenerator
{
    using System.Linq;
    using System.Text.Json;

    public class BooleanSchemaInstantiator : IPrimitiveSchemaInstantiator
    {
        private readonly bool[] values;
        private int valueIx;

        public BooleanSchemaInstantiator(JsonElement configElt)
        {
            values = configElt.EnumerateArray().Select(e => e.GetBoolean()).ToArray();
            valueIx = 0;
        }

        public void InstantiateSchema(Utf8JsonWriter jsonWriter)
        {
            bool value = values[valueIx];
            valueIx = (valueIx + 1) % values.Length;
            jsonWriter.WriteBooleanValue(value);
        }
    }
}
