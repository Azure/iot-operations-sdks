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

        private static ObjectType StandardizeSingleObject(string schemaText)
        {
            JsonSchemaStandardizer standardizer = new();

            return Assert.IsType<ObjectType>(
                Assert.Single(standardizer.GetStandardizedSchemas(schemaText, new CodeName("OpcUaAssets"), _ => throw new InvalidOperationException())));
        }
    }
}
