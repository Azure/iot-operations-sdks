namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.Collections.Generic;
    using System.IO;
    using Azure.Iot.Operations.CodeGeneration;

    public partial class RustCommandInvoker : IEnvoyTemplateTransform
    {
        private readonly CodeName commandName;
        private readonly CodeName genNamespace;
        private readonly EmptyTypeName serializerEmptyType;
        private readonly ITypeName? reqSchema;
        private readonly ITypeName? respSchema;
        private readonly List<CodeName> normalResultFields;
        private readonly List<CodeName> normalRequiredFields;
        private readonly CodeName? normalResultSchema;
        private readonly CodeName? errorResultName;
        private readonly CodeName? errorResultSchema;
        private readonly string topicPattern;
        private readonly bool doesCommandTargetExecutor;

        public RustCommandInvoker(
            CodeName commandName,
            CodeName genNamespace,
            EmptyTypeName serializerEmptyType,
            ITypeName? reqSchema,
            ITypeName? respSchema,
            List<CodeName> normalResultFields,
            List<CodeName> normalRequiredFields,
            CodeName? normalResultSchema,
            CodeName? errorResultName,
            CodeName? errorResultSchema,
            string topicPattern,
            bool doesCommandTargetExecutor)
        {
            this.commandName = commandName;
            this.genNamespace = genNamespace;
            this.serializerEmptyType = serializerEmptyType;
            this.reqSchema = reqSchema;
            this.respSchema = respSchema;
            this.normalResultFields = normalResultFields;
            this.normalRequiredFields = normalRequiredFields;
            this.normalResultSchema = normalResultSchema;
            this.errorResultName = errorResultName;
            this.errorResultSchema = errorResultSchema;
            this.topicPattern = topicPattern;
            this.doesCommandTargetExecutor = doesCommandTargetExecutor;
        }

        public string FileName { get => $"{this.commandName.GetFileName(TargetLanguage.Rust, "command", "invoker")}.rs"; }

        public string FolderPath { get => Path.Combine("src", this.genNamespace.GetFolderName(TargetLanguage.Rust)); }
    }
}
