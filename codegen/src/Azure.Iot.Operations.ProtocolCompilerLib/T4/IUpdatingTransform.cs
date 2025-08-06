namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    public interface IUpdatingTransform : ITemplateTransform
    {
        string FilePattern { get; }

        bool TryUpdateFile(string filePath);
    }
}
