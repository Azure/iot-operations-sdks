namespace Azure.Iot.Operations.ProtocolCompiler
{
    using DTDLParser.Models;

    public partial class CommandProto2 : ITemplateTransform
    {
        private readonly string projectName;
        private readonly CodeName genNamespace;
        private readonly ITypeName schema;
        private readonly string paramName;
        private readonly DTSchemaInfo paramSchema;
        private readonly bool isNullable;
        private readonly HashSet<string> importNames;

        public CommandProto2(string projectName, CodeName genNamespace, ITypeName schema, string paramName, DTSchemaInfo paramSchema, bool isNullable)
        {
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.schema = schema;
            this.paramName = paramName;
            this.paramSchema = paramSchema;
            this.isNullable = isNullable;
            this.importNames = new HashSet<string>();
        }

        public string FileName { get => $"{this.schema.GetFileName(TargetLanguage.Independent)}.proto"; }

        public string FolderPath { get => this.genNamespace.GetFileName(TargetLanguage.Independent); }
    }
}
