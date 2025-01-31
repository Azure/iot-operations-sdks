namespace Azure.Iot.Operations.ProtocolCompiler
{
    using DTDLParser.Models;

    public partial class ArrayAvroSchema : ITemplateTransform
    {
        private readonly DTSchemaInfo elementSchema;
        private readonly int indent;

        public ArrayAvroSchema(DTSchemaInfo elementSchema, int indent)
        {
            this.elementSchema = elementSchema;
            this.indent = indent;
        }

        public string FileName { get => string.Empty; }

        public string FolderPath { get => string.Empty; }
    }
}
