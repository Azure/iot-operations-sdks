namespace Azure.Iot.Operations.ProtocolCompiler
{
    using DTDLParser.Models;

    public partial class MapAvroSchema : ITemplateTransform
    {
        private readonly DTSchemaInfo valueSchema;
        private readonly int indent;

        public MapAvroSchema(DTSchemaInfo valueSchema, int indent)
        {
            this.valueSchema = valueSchema;
            this.indent = indent;
        }

        public string FileName { get => string.Empty; }

        public string FolderPath { get => string.Empty; }
    }
}
