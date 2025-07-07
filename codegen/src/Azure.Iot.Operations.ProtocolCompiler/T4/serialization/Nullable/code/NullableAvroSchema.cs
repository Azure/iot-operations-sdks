namespace Azure.Iot.Operations.ProtocolCompiler
{
    using DTDLParser;
    using DTDLParser.Models;

    public partial class NullableAvroSchema : ITemplateTransform
    {
        private readonly DTSchemaInfo schema;
        private readonly int indent;
        private readonly CodeName? sharedPrefix;
        private readonly HashSet<Dtmi> definedIds;
        private readonly int mqttVersion;

        public NullableAvroSchema(DTSchemaInfo schema, int indent, CodeName? sharedPrefix, HashSet<Dtmi> definedIds, int mqttVersion)
        {
            this.schema = schema;
            this.indent = indent;
            this.sharedPrefix = sharedPrefix;
            this.definedIds = definedIds;
            this.mqttVersion = mqttVersion;
        }

        public string FileName { get => string.Empty; }

        public string FolderPath { get => string.Empty; }
    }
}
