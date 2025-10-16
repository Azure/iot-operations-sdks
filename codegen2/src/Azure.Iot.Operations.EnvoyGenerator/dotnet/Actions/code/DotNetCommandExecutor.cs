namespace Azure.Iot.Operations.EnvoyGenerator
{
    using Azure.Iot.Operations.CodeGeneration;

    public partial class DotNetCommandExecutor : IEnvoyTemplateTransform
    {
        private readonly CodeName commandName;
        private readonly string projectName;
        private readonly CodeName genNamespace;
        private readonly string modelId;
        private readonly CodeName serviceName;
        private readonly string serializerClassName;
        private readonly EmptyTypeName serializerEmptyType;
        private readonly ITypeName? reqSchema;
        private readonly ITypeName? respSchema;
        private readonly string? serviceGroupId;
        private readonly string topicPattern;
        private readonly bool isIdempotent;

        public DotNetCommandExecutor(
            CodeName commandName,
            string projectName,
            CodeName genNamespace,
            string modelId,
            CodeName serviceName,
            string serializerClassName,
            EmptyTypeName serializerEmptyType,
            ITypeName? reqSchema,
            ITypeName? respSchema,
            string? serviceGroupId,
            string topicPattern,
            bool isIdempotent)
        {
            this.commandName = commandName;
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.modelId = modelId;
            this.serviceName = serviceName;
            this.serializerClassName = serializerClassName;
            this.serializerEmptyType = serializerEmptyType;
            this.reqSchema = reqSchema;
            this.respSchema = respSchema;
            this.serviceGroupId = serviceGroupId;
            this.topicPattern = topicPattern;
            this.isIdempotent = isIdempotent;
        }

        public string FileName { get => $"{this.commandName.GetFileName(TargetLanguage.CSharp, "command", "executor")}.g.cs"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.CSharp); }
    }
}
