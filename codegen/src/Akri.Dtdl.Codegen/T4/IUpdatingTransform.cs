namespace Akri.Dtdl.Codegen
{
    public interface IUpdatingTransform : ITemplateTransform
    {
        string FilePattern { get; }

        bool TryUpdateFile(string filePath);
    }
}
