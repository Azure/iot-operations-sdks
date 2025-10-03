public class DatasetWriteResponse : Dictionary<string, string>
{
    public DatasetWriteResponse(
        IReadOnlyDictionary<string, string> writeResponses)
        : base(writeResponses)
    {
    }
}

