namespace Akri.Dtdl.Codegen.IntegrationTests.TestCaseGenerator
{
    using Akri.Dtdl.Codegen.IntegrationTests.SchemaExtractor;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;

    public class MapSchemaInstantiator : ISchemaInstantiator
    {
        private readonly SchemaInstantiator schemaInstantiator;
        private readonly List<string>[] keySets;
        private int keySetIx;

        public MapSchemaInstantiator(JsonElement configElt, SchemaInstantiator schemaInstantiator)
        {
            this.schemaInstantiator = schemaInstantiator;
            keySets = configElt.EnumerateArray().Select(e => e.GetProperty("keys").EnumerateArray().Select(e => e.GetString()!).ToList()).ToArray();
            keySetIx = 0;
        }

        public void InstantiateSchema(Utf8JsonWriter jsonWriter, SchemaTypeInfo schemaType)
        {
            if (schemaType is not MapTypeInfo arrayType)
            {
                throw new Exception($"MapSchemaInstantiator does not support {schemaType.GetType()}");
            }

            List<string> keySet = keySets[keySetIx];
            keySetIx = (keySetIx + 1) % keySets.Length;

            jsonWriter.WriteStartObject();

            foreach (string key in keySet)
            {
                jsonWriter.WritePropertyName(key);
                schemaInstantiator.InstantiateSchema(jsonWriter, arrayType.ValueSchema);
            }

            jsonWriter.WriteEndObject();
        }
    }
}
