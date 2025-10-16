namespace Azure.Iot.Operations.SchemaGenerator
{
    using Azure.Iot.Operations.CodeGeneration;

    public partial class ObjectJsonSchema : ISchemaTemplateTransform
    {
        SchemaNamer schemaNamer;
        string schemaName;
        ObjectSpec objectSpec;
        string genNamespace;

        internal ObjectJsonSchema(SchemaNamer schemaNamer, string schemaName, ObjectSpec objectSpec, string genNamespace)
        {
            this.schemaNamer = schemaNamer;
            this.schemaName = schemaName;
            this.objectSpec = objectSpec;
            this.genNamespace = genNamespace;
        }

        public SerializationFormat Format { get => SerializationFormat.Json; }

        public string FileName { get => $"{this.schemaName}.schema.json"; }

        public string FolderPath { get => this.genNamespace; }
    }
}
