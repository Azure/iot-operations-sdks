namespace Akri.Dtdl.Codegen.IntegrationTests.TestCaseGenerator
{
    using Akri.Dtdl.Codegen.IntegrationTests.SchemaExtractor;
    using System;
    using System.Text.Json;

    public class SchemaInstantiator : ISchemaInstantiator
    {
        private readonly PrimitiveSchemaInstantiator primitiveSchemaInstantiator;
        private readonly ArraySchemaInstantiator arraySchemaInstantiator;
        private readonly ObjectSchemaInstantiator objectSchemaInstantiator;
        private readonly MapSchemaInstantiator mapSchemaInstantiator;
        private readonly EnumSchemaInstantiator enumSchemaInstantiator;

        public SchemaInstantiator(JsonElement configElt)
        {
            primitiveSchemaInstantiator = new PrimitiveSchemaInstantiator(configElt);
            arraySchemaInstantiator = new ArraySchemaInstantiator(configElt.GetProperty("Array"), this);
            objectSchemaInstantiator = new ObjectSchemaInstantiator(configElt.GetProperty("Object"), this);
            mapSchemaInstantiator = new MapSchemaInstantiator(configElt.GetProperty("Map"), this);
            enumSchemaInstantiator = new EnumSchemaInstantiator(configElt.GetProperty("Enum"));
        }

        public void InstantiateSchema(Utf8JsonWriter jsonWriter, SchemaTypeInfo schemaType)
        {
            switch (schemaType)
            {
                case PrimitiveTypeInfo _:
                    primitiveSchemaInstantiator.InstantiateSchema(jsonWriter, schemaType);
                    break;
                case ArrayTypeInfo _:
                    arraySchemaInstantiator.InstantiateSchema(jsonWriter, schemaType);
                    break;
                case ObjectTypeInfo _:
                    objectSchemaInstantiator.InstantiateSchema(jsonWriter, schemaType);
                    break;
                case MapTypeInfo _:
                    mapSchemaInstantiator.InstantiateSchema(jsonWriter, schemaType);
                    break;
                case EnumTypeInfo _:
                    enumSchemaInstantiator.InstantiateSchema(jsonWriter, schemaType);
                    break;
                default:
                    throw new Exception($"SchemaInstantiator does not support {schemaType.GetType()}");
            };
        }
    }
}
