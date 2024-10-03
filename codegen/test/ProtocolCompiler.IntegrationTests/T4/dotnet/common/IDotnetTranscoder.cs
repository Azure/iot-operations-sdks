using Azure.Iot.Operations.ProtocolCompiler.IntegrationTests.SchemaExtractor;

namespace Azure.Iot.Operations.ProtocolCompiler.IntegrationTests.T4
{
    public interface IDotnetTranscoder
    {
        bool CapitalizerNeeded { get; }

        bool DecapitalizerNeeded { get; }

        string EmptySchemaType { get; }

        string CheckPresence(string objName, string fieldName, SchemaTypeInfo schemaType);

        string JTokenFromSchemaField(string objName, string fieldName, SchemaTypeInfo schemaType);

        string AssignSchemaFieldFromJToken(string objName, string fieldName, string varName, SchemaTypeInfo schemaType);

        string JTokenFromSchemaValue(string varName, SchemaTypeInfo schemaType);

        string SchemaValueFromJToken(string varName, SchemaTypeInfo schemaType);
    }
}
