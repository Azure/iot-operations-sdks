namespace Azure.Iot.Operations.SchemaGenerator
{
    public partial class ObjectJsonSchema : ISchemaTemplateTransform
    {
        string schemaName;
        ObjectSpec objectSpec;
        string genNamespace;

        internal ObjectJsonSchema(string schemaName, ObjectSpec objectSpec, string genNamespace)
        {
            this.schemaName = schemaName;
            this.objectSpec = objectSpec;
            this.genNamespace = genNamespace;
        }

        public string FileName { get => $"{this.schemaName}.schema.json"; }

        public string FolderPath { get => this.genNamespace; }
    }
}
