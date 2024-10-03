namespace Azure.Iot.Operations.ProtocolCompiler.IntegrationTests.TestCaseGenerator
{
    using System.Text.Json;

    public interface IPrimitiveSchemaInstantiator
    {
        void InstantiateSchema(Utf8JsonWriter jsonWriter);
    }
}
