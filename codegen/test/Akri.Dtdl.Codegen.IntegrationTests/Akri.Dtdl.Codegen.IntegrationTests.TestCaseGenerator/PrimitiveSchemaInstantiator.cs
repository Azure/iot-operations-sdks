namespace Akri.Dtdl.Codegen.IntegrationTests.TestCaseGenerator
{
    using Akri.Dtdl.Codegen.IntegrationTests.SchemaExtractor;
    using System;
    using System.Text.Json;

    public class PrimitiveSchemaInstantiator : ISchemaInstantiator
    {
        private readonly BooleanSchemaInstantiator booleanSchemaInstantiator;
        private readonly StringishSchemaInstantiator dateSchemaInstantiator;
        private readonly StringishSchemaInstantiator dateTimeSchemaInstantiator;
        private readonly FractionalSchemaInstantiator doubleSchemaInstantiator;
        private readonly StringishSchemaInstantiator durationSchemaInstantiator;
        private readonly FractionalSchemaInstantiator floatSchemaInstantiator;
        private readonly IntegralSchemaInstantiator integerSchemaInstantiator;
        private readonly IntegralSchemaInstantiator longSchemaInstantiator;
        private readonly StringishSchemaInstantiator stringSchemaInstantiator;
        private readonly StringishSchemaInstantiator timeSchemaInstantiator;

        public PrimitiveSchemaInstantiator(JsonElement configElt)
        {
            booleanSchemaInstantiator = new BooleanSchemaInstantiator(configElt.GetProperty("Boolean"));
            dateSchemaInstantiator = new StringishSchemaInstantiator(configElt.GetProperty("Date"));
            dateTimeSchemaInstantiator = new StringishSchemaInstantiator(configElt.GetProperty("DateTime"));
            doubleSchemaInstantiator = new FractionalSchemaInstantiator(configElt.GetProperty("Double"));
            durationSchemaInstantiator = new StringishSchemaInstantiator(configElt.GetProperty("Duration"));
            floatSchemaInstantiator = new FractionalSchemaInstantiator(configElt.GetProperty("Float"));
            integerSchemaInstantiator = new IntegralSchemaInstantiator(configElt.GetProperty("Integer"));
            longSchemaInstantiator = new IntegralSchemaInstantiator(configElt.GetProperty("Long"));
            stringSchemaInstantiator = new StringishSchemaInstantiator(configElt.GetProperty("String"));
            timeSchemaInstantiator = new StringishSchemaInstantiator(configElt.GetProperty("Time"));
        }

        public void InstantiateSchema(Utf8JsonWriter jsonWriter, SchemaTypeInfo schemaType)
        {
            if (schemaType is not PrimitiveTypeInfo primitiveType)
            {
                throw new Exception($"PrimitiveSchemaInstantiator does not support {schemaType.GetType()}");
            }

            switch (primitiveType.SchemaName)
            {
                case "Boolean":
                    booleanSchemaInstantiator.InstantiateSchema(jsonWriter);
                    break;
                case "Date":
                    dateSchemaInstantiator.InstantiateSchema(jsonWriter);
                    break;
                case "DateTime":
                    dateTimeSchemaInstantiator.InstantiateSchema(jsonWriter);
                    break;
                case "Double":
                    doubleSchemaInstantiator.InstantiateSchema(jsonWriter);
                    break;
                case "Duration":
                    durationSchemaInstantiator.InstantiateSchema(jsonWriter);
                    break;
                case "Float":
                    floatSchemaInstantiator.InstantiateSchema(jsonWriter);
                    break;
                case "Integer":
                    integerSchemaInstantiator.InstantiateSchema(jsonWriter);
                    break;
                case "Long":
                    longSchemaInstantiator.InstantiateSchema(jsonWriter);
                    break;
                case "String":
                    stringSchemaInstantiator.InstantiateSchema(jsonWriter);
                    break;
                case "Time":
                    timeSchemaInstantiator.InstantiateSchema(jsonWriter);
                    break;
                default:
                    throw new Exception($"unrecognized primitive type schema name {primitiveType.SchemaName}");
            };

        }
    }
}
