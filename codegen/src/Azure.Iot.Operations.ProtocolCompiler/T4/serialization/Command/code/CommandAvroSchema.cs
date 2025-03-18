namespace Azure.Iot.Operations.ProtocolCompiler
{
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

        public CommandAvroSchema(string projectName, CodeName genNamespace, ITypeName schema, string commandName, string subType, string paramName, DTSchemaInfo paramSchema, CodeName? sharedPrefix, bool isNullable)
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
        }

        public string FileName { get => $"{this.schema.GetFileName(TargetLanguage.Independent)}.avsc"; }

        public string FolderPath { get => this.genNamespace.GetFileName(TargetLanguage.Independent); }
    }
}
