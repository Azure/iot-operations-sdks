namespace Azure.Iot.Operations.ProtocolCompiler.IntegrationTests.TestCaseGenerator
{
    using Azure.Iot.Operations.ProtocolCompiler.IntegrationTests.SchemaExtractor;
    using System;
    using System.Linq;
    using System.Text.Json;

    public class ArraySchemaInstantiator : ISchemaInstantiator
    {
        private readonly SchemaInstantiator schemaInstantiator;
        private readonly long[] lengths;
        private int lengthIx;

        public ArraySchemaInstantiator(JsonElement configElt, SchemaInstantiator schemaInstantiator)
        {
            this.schemaInstantiator = schemaInstantiator;
            lengths = configElt.EnumerateArray().Select(e => e.GetProperty("length").GetInt64()!).ToArray();
            lengthIx = 0;
        }

        public void InstantiateSchema(Utf8JsonWriter jsonWriter, SchemaTypeInfo schemaType)
        {
            if (schemaType is not ArrayTypeInfo arrayType)
            {
                throw new Exception($"ArraySchemaInstantiator does not support {schemaType.GetType()}");
            }

            long length = lengths[lengthIx];
            lengthIx = (lengthIx + 1) % lengths.Length;

            jsonWriter.WriteStartArray();

            for (int l = 0; l < length; ++l)
            {
                schemaInstantiator.InstantiateSchema(jsonWriter, arrayType.ElementSchema);
            }

            jsonWriter.WriteEndArray();
        }
    }
}
