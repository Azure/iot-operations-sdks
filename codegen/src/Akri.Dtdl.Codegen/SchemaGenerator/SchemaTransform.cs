namespace Akri.Dtdl.Codegen
{
    internal class SchemaTransform : ITemplateTransform
    {
        private readonly string schemaDefinition;

        public SchemaTransform(string folderPath, string fileName, string schemaDefinition)
        {
            this.FileName = fileName;
            this.FolderPath = folderPath;
            this.schemaDefinition = schemaDefinition;
        }

        public string FileName { get; }

        public string FolderPath { get; }

        public string TransformText()
        {
            return this.schemaDefinition;
        }
    }
}
