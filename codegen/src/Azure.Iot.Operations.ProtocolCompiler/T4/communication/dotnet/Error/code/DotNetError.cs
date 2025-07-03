namespace Azure.Iot.Operations.ProtocolCompiler
{
    public partial class DotNetError : ITemplateTransform
    {
        private readonly string projectName;
        private readonly CodeName schemaName;
        private readonly CodeName schemaNamespace;
        private readonly CodeName? errorCodeName;
        private readonly CodeName? errorCodeSchema;
        private readonly CodeName? errorInfoName;
        private readonly CodeName? errorInfoSchema;
        private readonly string description;
        private readonly CodeName? messageField;
        private readonly bool nullable;

        public DotNetError(string projectName, CodeName schemaName, CodeName schemaNamespace, CodeName? errorCodeName, CodeName? errorCodeSchema, CodeName? errorInfoName, CodeName? errorInfoSchema, string description, CodeName? messageField, bool nullable)
        {
            this.projectName = projectName;
            this.schemaName = schemaName;
            this.schemaNamespace = schemaNamespace;
            this.errorCodeName = errorCodeName;
            this.errorCodeSchema = errorCodeSchema;
            this.errorInfoName = errorInfoName;
            this.errorInfoSchema = errorInfoSchema;
            this.description = description;
            this.messageField = messageField;
            this.nullable = nullable;
        }

        public string FileName { get => $"{this.schemaName.GetFileName(TargetLanguage.CSharp, "exception")}.g.cs"; }

        public string FolderPath { get => this.schemaNamespace.GetFolderName(TargetLanguage.CSharp); }
    }
}
