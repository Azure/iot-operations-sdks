namespace Azure.Iot.Operations.TypeGenerator
{
    internal interface ITypeTemplateTransform
    {
        string FileName { get; }

        string FolderPath { get; }

        string TransformText();
    }
}
