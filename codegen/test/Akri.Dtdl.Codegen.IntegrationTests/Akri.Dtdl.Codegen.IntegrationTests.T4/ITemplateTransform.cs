namespace Akri.Dtdl.Codegen.IntegrationTests.T4
{
    public interface ITemplateTransform
    {
        string FileName { get; }

        string FolderPath { get; }

        string TransformText();
    }
}
