// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.EnvoyGenerator
{
    using Azure.Iot.Operations.CodeGeneration;

    public partial class DotNetCommandInvoker : IEnvoyTemplateTransform
    {
        private readonly CodeName commandName;
        private readonly CodeName componentName;
        private readonly string projectName;
        private readonly CodeName genNamespace;
        private readonly CodeName serviceName;
        private readonly string serializerClassName;
        private readonly EmptyTypeName serializerEmptyType;
        private readonly ITypeName? reqSchema;
        private readonly ITypeName? respSchema;
        private readonly string topicPattern;

        public DotNetCommandInvoker(
            string commandName,
            string componentName,
            string projectName,
            CodeName genNamespace,
            CodeName serviceName,
            string serializerClassName,
            EmptyTypeName serializerEmptyType,
            ITypeName? reqSchema,
            ITypeName? respSchema,
            string topicPattern)
        {
            this.commandName = new CodeName(commandName);
            this.componentName = new CodeName(componentName);
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.serviceName = serviceName;
            this.serializerClassName = serializerClassName;
            this.serializerEmptyType = serializerEmptyType;
            this.reqSchema = reqSchema;
            this.respSchema = respSchema;
            this.topicPattern = topicPattern;
        }

        public string FileName { get => $"{this.componentName.GetFileName(TargetLanguage.CSharp)}.g.cs"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.CSharp); }

        public EndpointTarget EndpointTarget { get => EndpointTarget.Client; }
    }
}
