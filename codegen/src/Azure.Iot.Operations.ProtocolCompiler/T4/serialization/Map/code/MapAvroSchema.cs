namespace Azure.Iot.Operations.ProtocolCompiler
{
    using DTDLParser.Models;

    public partial class MapAvroSchema : ITemplateTransform
    {
        private readonly DTSchemaInfo valueSchema;
        private readonly int indent;
        private readonly CodeName? sharedPrefix;

        public MapAvroSchema(DTSchemaInfo valueSchema, int indent, CodeName? sharedPrefix)
        {
            this.valueSchema = valueSchema;
            this.indent = indent;
            this.sharedPrefix = sharedPrefix;
        }

        public string FileName { get => string.Empty; }

        public string FolderPath { get => string.Empty; }
    }
}
