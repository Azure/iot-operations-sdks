namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.Collections.Generic;
    using System.IO;
    using Azure.Iot.Operations.CodeGeneration;

    public partial class RustCommandExecutorHeaders : IEnvoyTemplateTransform
    {
        private readonly CodeName commandName;
        private readonly CodeName genNamespace;
        private readonly CodeName errorCodeName;
        private readonly CodeName errorCodeSchema;
        private readonly CodeName? errorInfoName;
        private readonly CodeName? errorInfoSchema;
        List<string> errorCodeValues;

        public RustCommandExecutorHeaders(
            CodeName commandName,
            CodeName genNamespace,
            CodeName errorCodeName,
            CodeName errorCodeSchema,
            CodeName? errorInfoName,
            CodeName? errorInfoSchema,
            List<string> errorCodeValues)
        {
            this.commandName = commandName;
            this.genNamespace = genNamespace;
            this.errorCodeName = errorCodeName;
            this.errorCodeSchema = errorCodeSchema;
            this.errorInfoName = errorInfoName;
            this.errorInfoSchema = errorInfoSchema;
            this.errorCodeValues = errorCodeValues;
        }

        public string FileName { get => $"{this.commandName.GetFileName(TargetLanguage.Rust, "command", "executor", "headers")}.rs"; }

        public string FolderPath { get => Path.Combine("src", this.genNamespace.GetFolderName(TargetLanguage.Rust)); }
    }
}
