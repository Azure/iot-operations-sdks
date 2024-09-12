namespace Azure.Iot.Operations.ProtocolCompiler.IntegrationTests.T4
{
    public interface ITemplateTransform
    {
        string FileName { get; }

        string FolderPath { get; }

        string TransformText();
    }
}
