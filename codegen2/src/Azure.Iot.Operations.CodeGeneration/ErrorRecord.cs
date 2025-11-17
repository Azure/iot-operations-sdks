namespace Azure.Iot.Operations.CodeGeneration
{
    public record ErrorRecord(string Message, string Filename, int LineNumber, int CfLineNumber, string CrossRef);
}
