namespace Azure.Iot.Operations.ProtocolCompiler
{
    using DTDLParser.Models;

    public partial class ArrayAvroSchema : ITemplateTransform
    {
        private readonly DTSchemaInfo elementSchema;
        private readonly int indent;
        private readonly CodeName? sharedPrefix;

        public ArrayAvroSchema(DTSchemaInfo elementSchema, int indent, CodeName? sharedPrefix)
        {
            this.elementSchema = elementSchema;
            this.indent = indent;
            this.sharedPrefix = sharedPrefix;
        }

        public string FileName { get => string.Empty; }

        public string FolderPath { get => string.Empty; }
    }
}
