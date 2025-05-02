namespace Azure.Iot.Operations.ProtocolCompiler
{
    using DTDLParser;
    using DTDLParser.Models;

    public partial class ArrayAvroSchema : ITemplateTransform
    {
        private readonly CodeName schema;
        private readonly DTSchemaInfo elementSchema;
        private readonly int indent;
        private readonly CodeName? sharedPrefix;
        private readonly HashSet<Dtmi> definedIds;

        public ArrayAvroSchema(CodeName schema, DTSchemaInfo elementSchema, int indent, CodeName? sharedPrefix, HashSet<Dtmi> definedIds)
        {
            this.schema = schema;
            this.elementSchema = elementSchema;
            this.indent = indent;
            this.sharedPrefix = sharedPrefix;
            this.definedIds = definedIds;
        }

        public string FileName { get => string.Empty; }

        public string FolderPath { get => string.Empty; }
    }
}
