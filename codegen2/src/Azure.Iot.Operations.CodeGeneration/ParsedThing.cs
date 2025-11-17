namespace Azure.Iot.Operations.CodeGeneration
{
    using Azure.Iot.Operations.TDParser.Model;

    public record ParsedThing(TDThing Thing, string FileName, string DirectoryName, SchemaNamer SchemaNamer, ErrorReporter ErrorReporter);
}
