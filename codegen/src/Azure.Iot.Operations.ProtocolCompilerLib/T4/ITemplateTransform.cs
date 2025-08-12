namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    public interface ITemplateTransform
    {
        string FileName { get; }

        string FolderPath { get; }

        string TransformText();
    }
}
