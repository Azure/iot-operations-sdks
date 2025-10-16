namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.Collections.Generic;
    using Azure.Iot.Operations.CodeGeneration;

    public partial class DotNetCommandInvoker : IEnvoyTemplateTransform
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
        private readonly string topicPattern;

        public DotNetCommandInvoker(
            CodeName commandName,
            string projectName,
            CodeName genNamespace,
            string modelId,
            CodeName serviceName,
            string serializerClassName,
            EmptyTypeName serializerEmptyType,
            ITypeName? reqSchema,
            ITypeName? respSchema,
            string topicPattern)
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
            this.topicPattern = topicPattern;
        }

        public string FileName { get => $"{this.commandName.GetFileName(TargetLanguage.CSharp, "command", "invoker")}.g.cs"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.CSharp); }
    }
}
