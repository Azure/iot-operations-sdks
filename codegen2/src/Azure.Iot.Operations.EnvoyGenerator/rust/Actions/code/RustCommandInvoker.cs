namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.Collections.Generic;
    using System.IO;
    using Azure.Iot.Operations.CodeGeneration;

    public partial class RustCommandInvoker : IEnvoyTemplateTransform
    {
        private readonly CodeName commandName;
        private readonly CodeName componentName;
        private readonly CodeName genNamespace;
        private readonly EmptyTypeName serializerEmptyType;
        private readonly ITypeName? reqSchema;
        private readonly ITypeName? respSchema;
        private readonly List<CodeName> normalResultFields;
        private readonly List<CodeName> normalRequiredFields;
        private readonly ITypeName? normalResultSchema;
        private readonly CodeName? errorResultName;
        private readonly CodeName? errorResultSchema;
        private readonly string topicPattern;
        private readonly bool doesCommandTargetExecutor;
        private readonly string srcSubdir;

        public RustCommandInvoker(
            string commandName,
            string componentName,
            CodeName genNamespace,
            EmptyTypeName serializerEmptyType,
            ITypeName? reqSchema,
            ITypeName? respSchema,
            List<CodeName> normalResultFields,
            List<CodeName> normalRequiredFields,
            ITypeName? normalResultSchema,
            CodeName? errorResultName,
            CodeName? errorResultSchema,
            string topicPattern,
            bool doesCommandTargetExecutor,
            string srcSubdir)
        {
            this.commandName = new CodeName(commandName);
            this.componentName = new CodeName(componentName);
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
            this.srcSubdir = srcSubdir;
        }

        public string FileName { get => $"{this.componentName.GetFileName(TargetLanguage.Rust)}.rs"; }

        public string FolderPath { get => Path.Combine(this.srcSubdir, this.genNamespace.GetFolderName(TargetLanguage.Rust)); }

        public EndpointTarget EndpointTarget { get => EndpointTarget.Client; }
    }
}
