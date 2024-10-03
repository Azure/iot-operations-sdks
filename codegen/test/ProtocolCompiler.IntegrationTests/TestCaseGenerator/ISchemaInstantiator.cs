namespace Azure.Iot.Operations.ProtocolCompiler.IntegrationTests.TestCaseGenerator
{
    using Azure.Iot.Operations.ProtocolCompiler.IntegrationTests.SchemaExtractor;
    using System.Text.Json;

    public interface ISchemaInstantiator
    {
        void InstantiateSchema(Utf8JsonWriter jsonWriter, SchemaTypeInfo schemaType);
    }
}
