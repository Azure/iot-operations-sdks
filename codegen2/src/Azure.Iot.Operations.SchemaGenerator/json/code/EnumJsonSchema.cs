namespace Azure.Iot.Operations.SchemaGenerator
{
    using Azure.Iot.Operations.CodeGeneration;

    public partial class EnumJsonSchema : ISchemaTemplateTransform
    {
        string schemaName;
        EnumSpec enumSpec;

        internal EnumJsonSchema(string schemaName, EnumSpec enumSpec)
        {
            this.schemaName = schemaName;
            this.enumSpec = enumSpec;
        }

        public SerializationFormat Format { get => SerializationFormat.Json; }

        public string FileName { get => $"{this.schemaName}.schema.json"; }
    }
}
