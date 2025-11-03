namespace Azure.Iot.Operations.CodeGeneration
{
    using Azure.Iot.Operations.TDParser.Model;

    public record ParsedThing(TDThing Thing, string DirectoryName, SchemaNamer SchemaNamer);
}
