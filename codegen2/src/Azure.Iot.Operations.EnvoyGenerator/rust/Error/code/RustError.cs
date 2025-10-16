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

        public RustError(CodeName schemaName, CodeName genNamespace, string description, CodeName? messageField, bool messageIsRequired)
        {
            this.schemaName = schemaName;
            this.genNamespace = genNamespace;
            this.description = description;
            this.messageField = messageField;
            this.messageIsRequired = messageIsRequired;
        }

        public string FileName { get => $"{this.schemaName.GetFileName(TargetLanguage.Rust, "error")}.rs"; }

        public string FolderPath { get => Path.Combine("src", this.genNamespace.GetFolderName(TargetLanguage.Rust)); }
    }
}
