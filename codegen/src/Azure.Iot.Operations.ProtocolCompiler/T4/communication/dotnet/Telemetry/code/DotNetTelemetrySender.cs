namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System;

    public partial class DotNetTelemetrySender : ITemplateTransform
    {
        private readonly CodeName telemetryName;
        private readonly string projectName;
        private readonly CodeName genNamespace;
        private readonly string modelId;
        private readonly CodeName serviceName;
        private readonly string serializerSubNamespace;
        private readonly string serializerClassName;
        private readonly ITypeName schemaType;
        private readonly CodeName componentName;

        public DotNetTelemetrySender(CodeName telemetryName, string projectName, CodeName genNamespace, string modelId, CodeName serviceName, string serializerSubNamespace, string serializerClassName, EmptyTypeName serializerEmptyType, ITypeName schemaType)
        {
            this.telemetryName = telemetryName;
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.modelId = modelId;
            this.serviceName = serviceName;
            this.serializerSubNamespace = serializerSubNamespace;
            this.serializerClassName = string.Format(serializerClassName, $"<{schemaType.GetTypeName(TargetLanguage.CSharp)}, {serializerEmptyType.GetTypeName(TargetLanguage.CSharp)}>");
            this.schemaType = schemaType;
            this.componentName = new CodeName(this.telemetryName, "telemetry", "sender");
        }

        public string FileName { get => $"{this.componentName.GetFileName(TargetLanguage.CSharp)}.g.cs"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.CSharp); }
    }
}
