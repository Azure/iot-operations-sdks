namespace Azure.Iot.Operations.ProtocolCompiler.UnitTests.TypeGeneratorTests
{
    using Azure.Iot.Operations.ProtocolCompilerLib;

    public class JsonSchemaStandardizerTests
    {
        private const string RootPath = "../../../TypeGeneratorTests";

        [Fact]
        public void ResolvesRfc6901EncodedDefinitionReference()
        {
            string schemaText = File.ReadAllText(Path.Combine(RootPath, "MinimalTelemetry.schema.json"));

            ObjectType telemetryType = StandardizeSingleObject(schemaText);
            ObjectType.FieldInfo temperatureField = Assert.Single(telemetryType.FieldInfos).Value;

            Assert.IsType<IntegerType>(temperatureField.SchemaType);
        }

        [Theory]
        [InlineData("value~0name", "value~name")]
        [InlineData("value~01name", "value~1name")]
        public void ResolvesRfc6901EncodedDefinitionReferences(string referenceToken, string definitionName)
        {
            string schemaText = $$"""
                {
                  "$schema": "http://json-schema.org/draft-07/schema#",
                  "title": "MinimalTelemetry",
                  "type": "object",
                  "properties": {
                    "Temperature": { "$ref": "#/definitions/{{referenceToken}}" }
                  },
                  "definitions": {
                    "{{definitionName}}": {
                      "title": "Temperature",
                      "type": "integer",
                      "maximum": 2147483647
                    }
                  }
                }
                """;

            ObjectType telemetryType = StandardizeSingleObject(schemaText);
            ObjectType.FieldInfo temperatureField = Assert.Single(telemetryType.FieldInfos).Value;

            Assert.IsType<IntegerType>(temperatureField.SchemaType);
        }

        [Theory]
        [InlineData("{}")]
        [InlineData("true")]
        [InlineData("""{ "description": "any value" }""")]
        [InlineData("""{ "type": ["string", "number"] }""")]
        public void MapsUntypedFieldToAnyType(string fieldSchema)
        {
            Assert.IsType<AnyType>(StandardizeSingleField(fieldSchema).SchemaType);
        }

        [Theory]
        [InlineData("""{ "type": "array" }""")]
        [InlineData("""{ "type": "array", "items": {} }""")]
        [InlineData("""{ "type": "array", "items": true }""")]
        public void MapsArrayOfUntypedItemsToAnyElementType(string fieldSchema)
        {
            ArrayType arrayType = Assert.IsType<ArrayType>(StandardizeSingleField(fieldSchema).SchemaType);

            Assert.IsType<AnyType>(arrayType.ElementSchema);
        }

        [Theory]
        [InlineData("""{ "type": "object" }""")]
        [InlineData("""{ "type": "object", "additionalProperties": true }""")]
        public void MapsObjectWithoutTypedValuesToMapOfAnyType(string fieldSchema)
        {
            MapType mapType = Assert.IsType<MapType>(StandardizeSingleField(fieldSchema).SchemaType);

            Assert.IsType<AnyType>(mapType.ValueSchema);
        }

        [Fact]
        public void MapsNullableTypeListToUnderlyingType()
        {
            Assert.IsType<IntegerType>(StandardizeSingleField("""{ "type": ["integer", "null"], "maximum": 2147483647 }""").SchemaType);
        }

        [Fact]
        public void MapsRepeatedTypeListToUnderlyingType()
        {
            Assert.IsType<StringType>(StandardizeSingleField("""{ "type": ["string", "string"] }""").SchemaType);
        }

        [Theory]
        [InlineData("false")]
        [InlineData("""{ "type": null }""")]
        [InlineData("""{ "type": [] }""")]
        [InlineData("""{ "type": ["null"] }""")]
        [InlineData("""{ "type": ["string", 7] }""")]
        [InlineData("""{ "type": ["string", "not-a-json-schema-type"] }""")]
        public void RejectsSchemaThatNamesNoValueType(string fieldSchema)
        {
            Assert.Throws<Exception>(() => StandardizeSingleField(fieldSchema));
        }

        private static ObjectType.FieldInfo StandardizeSingleField(string fieldSchema)
        {
            string schemaText = $$"""
                {
                  "$schema": "http://json-schema.org/draft-07/schema#",
                  "title": "MinimalTelemetry",
                  "type": "object",
                  "properties": {
                    "Value": {{fieldSchema}}
                  }
                }
                """;

            return Assert.Single(StandardizeSingleObject(schemaText).FieldInfos).Value;
        }

        private static ObjectType StandardizeSingleObject(string schemaText)
        {
            JsonSchemaStandardizer standardizer = new();

            return Assert.IsType<ObjectType>(
                Assert.Single(standardizer.GetStandardizedSchemas(schemaText, new CodeName("OpcUaAssets"), _ => throw new InvalidOperationException())));
        }
    }
}
