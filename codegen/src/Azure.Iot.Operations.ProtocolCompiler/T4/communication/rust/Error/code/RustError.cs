namespace Azure.Iot.Operations.ProtocolCompiler
{
    public partial class RustError : ITemplateTransform
    {
        private readonly CodeName schemaName;
        private readonly CodeName schemaNamespace;
        private readonly string description;
        private readonly CodeName? messageField;
        private readonly bool nullable;

        public RustError(CodeName schemaName, CodeName schemaNamespace, string description, CodeName? messageField, bool nullable)
        {
            this.schemaName = schemaName;
            this.schemaNamespace = schemaNamespace;
            this.description = description;
            this.messageField = messageField;
            this.nullable = nullable;
        }

        public string FileName { get => $"{this.schemaName.GetFileName(TargetLanguage.Rust, "error")}.rs"; }

        public string FolderPath { get => this.schemaNamespace.GetFolderName(TargetLanguage.Rust); }
    }
}
