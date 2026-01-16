namespace Dtdl2Wot
{
    public interface ITemplateTransform
    {
        string FileName { get; }

        string FolderPath { get; }

        string TransformText();
    }
}
