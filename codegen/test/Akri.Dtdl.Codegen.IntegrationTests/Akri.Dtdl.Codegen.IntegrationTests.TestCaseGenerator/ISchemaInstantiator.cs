namespace Akri.Dtdl.Codegen.IntegrationTests.TestCaseGenerator
{
    using Akri.Dtdl.Codegen.IntegrationTests.SchemaExtractor;
    using System.Text.Json;

    public interface ISchemaInstantiator
    {
        void InstantiateSchema(Utf8JsonWriter jsonWriter, SchemaTypeInfo schemaType);
    }
}
