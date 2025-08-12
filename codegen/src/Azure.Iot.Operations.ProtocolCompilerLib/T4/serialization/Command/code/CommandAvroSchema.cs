namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    using DTDLParser;
    using DTDLParser.Models;

    public partial class CommandAvroSchema : ITemplateTransform
    {
        private readonly string projectName;
        private readonly CodeName genNamespace;
        private readonly ITypeName schema;
        private readonly string commandName;
        private readonly string subType;
        private readonly string paramName;
        private readonly DTSchemaInfo paramSchema;
        private readonly CodeName? sharedPrefix;
        private readonly bool isNullable;
        private readonly HashSet<Dtmi> definedIds;
        private readonly int mqttVersion;

        public CommandAvroSchema(string projectName, CodeName genNamespace, ITypeName schema, string commandName, string subType, string paramName, DTSchemaInfo paramSchema, CodeName? sharedPrefix, int mqttVersion, bool isNullable)
        {
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.schema = schema;
            this.commandName = commandName;
            this.subType = subType;
            this.paramName = paramName;
            this.paramSchema = paramSchema;
            this.sharedPrefix = sharedPrefix;
            this.isNullable = isNullable;
            this.definedIds = new HashSet<Dtmi>();
            this.mqttVersion = mqttVersion;
        }

        public string FileName { get => $"{this.schema.GetFileName(TargetLanguage.Independent)}.avsc"; }

        public string FolderPath { get => this.genNamespace.GetFileName(TargetLanguage.Independent); }
    }
}
