namespace Azure.Iot.Operations.ProtocolCompiler
{
    public interface ITemplateTransform
    {
        string FileName { get; }

        string FolderPath { get; }

        string TransformText();
    }
}
