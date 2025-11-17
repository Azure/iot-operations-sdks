namespace Azure.Iot.Operations.TypeGenerator
{
    using System.Text.Json;
    using Azure.Iot.Operations.CodeGeneration;

    public record SchemaRoot(JsonTracker JsonTracker, string FileName, string DirectoryName, ErrorReporter ErrorReporter);
}
