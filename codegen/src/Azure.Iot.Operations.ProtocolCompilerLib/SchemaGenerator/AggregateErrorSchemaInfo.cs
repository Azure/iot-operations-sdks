namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    public record AggregateErrorSchemaInfo(CodeName Schema, List<(string, CodeName)> InnerErrors)
    {
    }
}
