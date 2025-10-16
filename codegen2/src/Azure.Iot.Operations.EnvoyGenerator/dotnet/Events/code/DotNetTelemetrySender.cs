namespace Azure.Iot.Operations.EnvoyGenerator
{
    using Azure.Iot.Operations.CodeGeneration;

    public partial class DotNetTelemetrySender : IEnvoyTemplateTransform
    {
        private readonly CodeName telemetryName;
        private readonly CodeName componentName;
        private readonly string projectName;
        private readonly CodeName genNamespace;
        private readonly string modelId;
        private readonly CodeName serviceName;
        private readonly string serializerClassName;
        private readonly ITypeName schemaType;
        private readonly string topicPattern;

        public DotNetTelemetrySender(
            string telemetryName,
            string componentName,
            string projectName,
            CodeName genNamespace,
            string modelId,
            CodeName serviceName,
            string serializerClassName,
            EmptyTypeName serializerEmptyType,
            ITypeName schemaType,
            string topicPattern)
        {
            this.telemetryName = new CodeName(telemetryName);
            this.componentName = new CodeName(componentName);
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.modelId = modelId;
            this.serviceName = serviceName;
            this.serializerClassName = string.Format(serializerClassName, $"<{schemaType.GetTypeName(TargetLanguage.CSharp)}, {serializerEmptyType.GetTypeName(TargetLanguage.CSharp)}>");
            this.schemaType = schemaType;
            this.topicPattern = topicPattern;
        }

        public string FileName { get => $"{this.componentName.GetFileName(TargetLanguage.CSharp)}.g.cs"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.CSharp); }
    }
}
