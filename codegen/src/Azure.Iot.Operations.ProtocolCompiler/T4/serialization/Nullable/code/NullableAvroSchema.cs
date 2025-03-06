namespace Azure.Iot.Operations.ProtocolCompiler
{
    using DTDLParser.Models;

    public partial class NullableAvroSchema : ITemplateTransform
    {
        private readonly DTSchemaInfo schema;
        private readonly int indent;
        private readonly CodeName? sharedPrefix;

        public NullableAvroSchema(DTSchemaInfo schema, int indent, CodeName? sharedPrefix)
        {
            this.schema = schema;
            this.indent = indent;
            this.sharedPrefix = sharedPrefix;
        }

        public string FileName { get => string.Empty; }

        public string FolderPath { get => string.Empty; }
    }
}
