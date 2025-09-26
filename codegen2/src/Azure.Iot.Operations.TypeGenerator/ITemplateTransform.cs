namespace Azure.Iot.Operations.TypeGenerator
{
    public interface ITemplateTransform
    {
        string FileName { get; }

        string FolderPath { get; }

        string TransformText();
    }
}
