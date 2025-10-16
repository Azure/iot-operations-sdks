namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.Collections.Generic;
    using System.IO;
    using Azure.Iot.Operations.CodeGeneration;

    public partial class RustCommandExecutor : IEnvoyTemplateTransform
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
        private readonly bool isIdempotent;
        private readonly string? serviceGroupId;
        private readonly string topicPattern;

        public RustCommandExecutor(
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
            bool isIdempotent,
            string? serviceGroupId,
            string topicPattern)
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
            this.isIdempotent = isIdempotent;
            this.serviceGroupId = serviceGroupId;
            this.topicPattern = topicPattern;
        }

        public string FileName { get => $"{this.commandName.GetFileName(TargetLanguage.Rust, "command", "executor")}.rs"; }

        public string FolderPath { get => Path.Combine("src", this.genNamespace.GetFolderName(TargetLanguage.Rust)); }
    }
}
