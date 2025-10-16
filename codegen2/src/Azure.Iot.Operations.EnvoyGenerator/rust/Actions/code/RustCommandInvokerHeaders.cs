namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.Collections.Generic;
    using System.IO;
    using Azure.Iot.Operations.CodeGeneration;

    public partial class RustCommandInvokerHeaders : IEnvoyTemplateTransform
    {
        private readonly CodeName commandName;
        private readonly CodeName componentName;
        private readonly CodeName genNamespace;
        private readonly CodeName errorCodeName;
        private readonly CodeName errorCodeSchema;
        private readonly CodeName? errorInfoName;
        private readonly CodeName? errorInfoSchema;
        List<string> errorCodeValues;
        private readonly string srcSubdir;

        public RustCommandInvokerHeaders(
            string commandName,
            string componentName,
            CodeName genNamespace,
            CodeName errorCodeName,
            CodeName errorCodeSchema,
            CodeName? errorInfoName,
            CodeName? errorInfoSchema,
            List<string> errorCodeValues,
            string srcSubdir)
        {
            this.commandName = new CodeName(commandName);
            this.componentName = new CodeName(componentName);
            this.genNamespace = genNamespace;
            this.errorCodeName = errorCodeName;
            this.errorCodeSchema = errorCodeSchema;
            this.errorInfoName = errorInfoName;
            this.errorInfoSchema = errorInfoSchema;
            this.errorCodeValues = errorCodeValues;
            this.srcSubdir = srcSubdir;
        }

        public string FileName { get => $"{this.componentName.GetFileName(TargetLanguage.Rust, "headers")}.rs"; }

        public string FolderPath { get => Path.Combine(this.srcSubdir, this.genNamespace.GetFolderName(TargetLanguage.Rust)); }
    }
}
