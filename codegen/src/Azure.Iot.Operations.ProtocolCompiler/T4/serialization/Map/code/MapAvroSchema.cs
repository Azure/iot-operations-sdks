namespace Azure.Iot.Operations.ProtocolCompiler
{
    using DTDLParser;
    using DTDLParser.Models;

    public partial class MapAvroSchema : ITemplateTransform
    {
        private readonly CodeName schema;
        private readonly DTSchemaInfo valueSchema;
        private readonly int indent;
        private readonly CodeName? sharedPrefix;
        private readonly HashSet<Dtmi> definedIds;

        public MapAvroSchema(CodeName schema, DTSchemaInfo valueSchema, int indent, CodeName? sharedPrefix, HashSet<Dtmi> definedIds)
        {
            this.schema = schema;
            this.valueSchema = valueSchema;
            this.indent = indent;
            this.sharedPrefix = sharedPrefix;
            this.definedIds = definedIds;
        }

        public string FileName { get => string.Empty; }

        public string FolderPath { get => string.Empty; }
    }
}
