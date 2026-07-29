namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    public partial class RustAggregateError : ITemplateTransform
    {
        private readonly CodeName schemaName;
        private readonly CodeName schemaNamespace;
        private readonly List<(CodeName, CodeName)> innerNameSchemas;

        public RustAggregateError(CodeName schemaName, CodeName schemaNamespace, List<(CodeName, CodeName)> innerNameSchemas)
        {
            this.schemaName = schemaName;
            this.schemaNamespace = schemaNamespace;
            this.innerNameSchemas = innerNameSchemas;
        }

        public string FileName { get => $"{this.schemaName.GetFileName(TargetLanguage.Rust, "error")}.rs"; }

        public string FolderPath { get => this.schemaNamespace.GetFolderName(TargetLanguage.Rust); }
    }
}
