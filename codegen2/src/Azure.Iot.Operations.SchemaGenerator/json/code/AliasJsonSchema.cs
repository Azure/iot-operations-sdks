namespace Azure.Iot.Operations.SchemaGenerator
{
    using System.IO;
    using Azure.Iot.Operations.CodeGeneration;

    public partial class AliasJsonSchema : ISchemaTemplateTransform
    {
        private readonly JsonSchemaSupport schemaSupport;
        private readonly string schemaName;
        private readonly AliasSpec aliasSpec;

        internal AliasJsonSchema(JsonSchemaSupport schemaSupport, string schemaName, AliasSpec aliasSpec)
        {
            this.schemaSupport = schemaSupport;
            this.schemaName = schemaName;
            this.aliasSpec = aliasSpec;
        }

        public SerializationFormat Format { get => SerializationFormat.Json; }

        public string FileName { get => $"{this.schemaName}.json"; }
    }
}
