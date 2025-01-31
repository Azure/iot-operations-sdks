namespace Azure.Iot.Operations.ProtocolCompiler
{
    using DTDLParser.Models;

    public partial class NullableAvroSchema : ITemplateTransform
    {
        private readonly DTSchemaInfo schema;
        private readonly int indent;

        public NullableAvroSchema(DTSchemaInfo schema, int indent)
        {
            this.schema = schema;
            this.indent = indent;
        }

        public string FileName { get => string.Empty; }

        public string FolderPath { get => string.Empty; }
    }
}
