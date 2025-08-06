
namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    public partial class RustCommandExecutorHeaders : ITemplateTransform
    {
        private readonly CodeName commandName;
        private readonly CodeName genNamespace;
        private readonly CodeName errorCodeName;
        private readonly CodeName errorCodeSchema;
        private readonly CodeName? errorInfoName;
        private readonly CodeName? errorInfoSchema;
        Dictionary<CodeName, string> errorCodeEnumeration;

        public RustCommandExecutorHeaders(
            CodeName commandName,
            CodeName genNamespace,
            CodeName errorCodeName,
            CodeName errorCodeSchema,
            CodeName? errorInfoName,
            CodeName? errorInfoSchema,
            Dictionary<CodeName, string> errorCodeEnumeration)
        {
            this.commandName = commandName;
            this.genNamespace = genNamespace;
            this.errorCodeName = errorCodeName;
            this.errorCodeSchema = errorCodeSchema;
            this.errorInfoName = errorInfoName;
            this.errorInfoSchema = errorInfoSchema;
            this.errorCodeEnumeration = errorCodeEnumeration;
        }

        public string FileName { get => $"{this.commandName.GetFileName(TargetLanguage.Rust, "command", "executor", "headers")}.rs"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.Rust); }
    }
}
