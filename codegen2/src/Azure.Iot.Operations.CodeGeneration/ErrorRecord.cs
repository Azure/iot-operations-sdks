namespace Azure.Iot.Operations.CodeGeneration
{
    public record ErrorRecord(ErrorCondition Condition, string Message, string Filename, int LineNumber, int CfLineNumber, string CrossRef);
}
