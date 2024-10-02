namespace Azure.Iot.Operations.ProtocolCompiler.IntegrationTests.TestCaseGenerator
{
    using Azure.Iot.Operations.ProtocolCompiler.IntegrationTests.SchemaExtractor;
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    public class ObjectSchemaInstantiator : ISchemaInstantiator
    {
        private readonly SchemaInstantiator schemaInstantiator;

        public ObjectSchemaInstantiator(JsonElement configElt, SchemaInstantiator schemaInstantiator)
        {
            this.schemaInstantiator = schemaInstantiator;
        }

        public void InstantiateSchema(Utf8JsonWriter jsonWriter, SchemaTypeInfo schemaType)
        {
            if (schemaType is not ObjectTypeInfo objectType)
            {
                throw new Exception($"ObjectSchemaInstantiator does not support {schemaType.GetType()}");
            }

            jsonWriter.WriteStartObject();

            foreach (KeyValuePair<string, SchemaTypeInfo> fieldSchema in objectType.FieldSchemas)
            {
                jsonWriter.WritePropertyName(fieldSchema.Key);
                schemaInstantiator.InstantiateSchema(jsonWriter, fieldSchema.Value);
            }

            jsonWriter.WriteEndObject();
        }
    }
}
