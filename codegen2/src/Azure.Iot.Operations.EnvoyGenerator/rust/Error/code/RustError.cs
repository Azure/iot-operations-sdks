namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.IO;
    using Azure.Iot.Operations.CodeGeneration;

    public partial class RustError : IEnvoyTemplateTransform
    {
        private readonly CodeName schemaName;
        private readonly CodeName genNamespace;
        private readonly string description;
        private readonly CodeName? messageField;
        private readonly bool messageIsRequired;
        private readonly string srcSubdir;

        public RustError(CodeName schemaName, CodeName genNamespace, string description, CodeName? messageField, bool messageIsRequired, string srcSubdir)
        {
            this.schemaName = schemaName;
            this.genNamespace = genNamespace;
            this.description = description;
            this.messageField = messageField;
            this.messageIsRequired = messageIsRequired;
            this.srcSubdir = srcSubdir;
        }

        public string FileName { get => $"{this.schemaName.GetFileName(TargetLanguage.Rust, "error")}.rs"; }

        public string FolderPath { get => Path.Combine(this.srcSubdir, this.genNamespace.GetFolderName(TargetLanguage.Rust)); }
    }
}
