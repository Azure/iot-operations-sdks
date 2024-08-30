namespace Akri.Dtdl.Codegen.IntegrationTests.TestCaseGenerator
{
    using System.Text.Json;

    public interface IPrimitiveSchemaInstantiator
    {
        void InstantiateSchema(Utf8JsonWriter jsonWriter);
    }
}
