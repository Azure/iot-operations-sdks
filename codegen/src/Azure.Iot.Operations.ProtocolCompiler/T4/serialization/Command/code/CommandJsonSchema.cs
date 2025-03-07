namespace Azure.Iot.Operations.ProtocolCompiler
{
    using DTDLParser.Models;

    public partial class CommandJsonSchema : ITemplateTransform
    {
        private readonly CodeName genNamespace;
        private readonly string schemaId;
        private readonly ITypeName schema;
        private readonly string commandName;
        private readonly string subType;
        private readonly string paramName;
        private readonly DTSchemaInfo paramSchema;
        private readonly CodeName? sharedPrefix;
        private readonly bool isNullable;
        private readonly bool setIndex;

        public CommandJsonSchema(CodeName genNamespace, string schemaId, ITypeName schema, string commandName, string subType, string paramName, DTSchemaInfo paramSchema, CodeName? sharedPrefix, bool isNullable, bool setIndex)
        {
            this.genNamespace = genNamespace;
            this.schemaId = schemaId;
            this.schema = schema;
            this.commandName = commandName;
            this.subType = subType;
            this.paramName = paramName;
            this.paramSchema = paramSchema;
            this.sharedPrefix = sharedPrefix;
            this.isNullable = isNullable;
            this.setIndex = setIndex;
        }

        public string FileName { get => $"{this.schema.GetFileName(TargetLanguage.Independent)}.schema.json"; }

        public string FolderPath { get => this.genNamespace.GetFileName(TargetLanguage.Independent); }
    }
}
