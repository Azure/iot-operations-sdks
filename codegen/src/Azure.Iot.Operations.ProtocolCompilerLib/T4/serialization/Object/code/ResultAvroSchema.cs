namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    using DTDLParser;
    using DTDLParser.Models;

    public partial class ResultAvroSchema : ITemplateTransform
    {
        private readonly string projectName;
        private readonly CodeName genNamespace;
        private readonly ITypeName schema;
        private readonly List<(string, string, DTSchemaInfo, bool, bool, int)> nameDescSchemaIndirectRequiredIndices;
        private readonly CodeName? sharedPrefix;
        private readonly HashSet<Dtmi> definedIds;
        private readonly int mqttVersion;

        public ResultAvroSchema(string projectName, CodeName genNamespace, ITypeName schema, List<(string, string, DTSchemaInfo, bool, bool, int)> nameDescSchemaIndirectRequiredIndices, CodeName? sharedPrefix, int mqttVersion)
        {
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.schema = schema;
            this.nameDescSchemaIndirectRequiredIndices = nameDescSchemaIndirectRequiredIndices;
            this.sharedPrefix = sharedPrefix;
            this.definedIds = new HashSet<Dtmi>();
            this.mqttVersion = mqttVersion;
        }

        public string FileName { get => $"{this.schema.GetFileName(TargetLanguage.Independent)}.avsc"; }

        public string FolderPath { get => this.genNamespace.GetFileName(TargetLanguage.Independent); }
    }
}
