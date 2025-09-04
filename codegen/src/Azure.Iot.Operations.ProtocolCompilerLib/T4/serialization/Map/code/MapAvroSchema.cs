namespace Azure.Iot.Operations.ProtocolCompilerLib
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
        private readonly int mqttVersion;
        private readonly bool nullValues;

        public MapAvroSchema(CodeName schema, DTSchemaInfo valueSchema, int indent, CodeName? sharedPrefix, HashSet<Dtmi> definedIds, int mqttVersion, bool nullValues)
        {
            this.schema = schema;
            this.valueSchema = valueSchema;
            this.indent = indent;
            this.sharedPrefix = sharedPrefix;
            this.definedIds = definedIds;
            this.mqttVersion = mqttVersion;
            this.nullValues = nullValues;
        }

        public string FileName { get => string.Empty; }

        public string FolderPath { get => string.Empty; }
    }
}
