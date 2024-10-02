namespace Azure.Iot.Operations.ProtocolCompiler
{
    public interface IUpdatingTransform : ITemplateTransform
    {
        string FilePattern { get; }

        bool TryUpdateFile(string filePath);
    }
}
