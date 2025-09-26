namespace Azure.Iot.Operations.SchemaGenerator
{
    public partial class EnumJsonSchema : ISchemaTemplateTransform
    {
        string schemaName;
        EnumSpec enumSpec;
        string genNamespace;

        internal EnumJsonSchema(string schemaName, EnumSpec enumSpec, string genNamespace)
        {
            this.schemaName = schemaName;
            this.enumSpec = enumSpec;
            this.genNamespace = genNamespace;
        }

        public string FileName { get => $"{this.schemaName}.schema.json"; }

        public string FolderPath { get => this.genNamespace; }
    }
}
