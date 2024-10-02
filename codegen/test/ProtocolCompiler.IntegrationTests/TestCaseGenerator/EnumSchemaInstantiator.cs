namespace Azure.Iot.Operations.ProtocolCompiler.IntegrationTests.TestCaseGenerator
{
    using Azure.Iot.Operations.ProtocolCompiler.IntegrationTests.SchemaExtractor;
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    public class EnumSchemaInstantiator : ISchemaInstantiator
    {
        private readonly Dictionary<string, int> enumIxes;

        public EnumSchemaInstantiator(JsonElement configElt)
        {
            enumIxes = new Dictionary<string, int>();
        }

        public void InstantiateSchema(Utf8JsonWriter jsonWriter, SchemaTypeInfo schemaType)
        {
            if (schemaType is not EnumTypeInfo enumType)
            {
                throw new Exception($"EnumSchemaInstantiator does not support {schemaType.GetType()}");
            }

            if (!enumIxes.TryGetValue(enumType.SchemaName, out int enumIx))
            {
                enumIx = 0;
            }

            jsonWriter.WriteStringValue(enumType.EnumNames[enumIx]);

            enumIxes[enumType.SchemaName] = (enumIx + 1) % enumType.EnumNames.Length;
        }
    }
}
