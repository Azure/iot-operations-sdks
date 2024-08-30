namespace Akri.Dtdl.Codegen
{
    public interface ITypeGenerator
    {
        void GenerateTypeFromSchema(string projectName, string genNamespace, SchemaType schemaType, string outputFolder, HashSet<string> sourceFilePaths);
    }
}
