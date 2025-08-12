namespace Azure.Iot.Operations.ProtocolCompilerLib
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
        private readonly int mqttVersion;

        public ArrayAvroSchema(CodeName schema, DTSchemaInfo elementSchema, int indent, CodeName? sharedPrefix, HashSet<Dtmi> definedIds, int mqttVersion)
        {
            this.schema = schema;
            this.elementSchema = elementSchema;
            this.indent = indent;
            this.sharedPrefix = sharedPrefix;
            this.definedIds = definedIds;
            this.mqttVersion = mqttVersion;
        }

        public string FileName { get => string.Empty; }

        public string FolderPath { get => string.Empty; }
    }
}
