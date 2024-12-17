namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System;

    public partial class DotNetTelemetryReceiver : ITemplateTransform
    {
        private readonly string? telemetryName;
        private readonly string projectName;
        private readonly string genNamespace;
        private readonly string modelId;
        private readonly string serviceName;
        private readonly string serializerSubNamespace;
        private readonly string serializerClassName;
        private readonly string schemaClassName;
        private readonly string componentName;

        public DotNetTelemetryReceiver(string? telemetryName, string projectName, string genNamespace, string modelId, string serviceName, string serializerSubNamespace, string serializerClassName, string serializerEmptyType, string schemaClassName)
        {
            this.telemetryName = telemetryName;
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.modelId = modelId;
            this.serviceName = serviceName;
            this.serializerSubNamespace = serializerSubNamespace;
            this.serializerClassName = string.Format(serializerClassName, $"<{schemaClassName}, {serializerEmptyType}>");
            this.schemaClassName = schemaClassName == "" ? "byte[]" : schemaClassName;
            this.componentName = schemaClassName == "" ? $"{(this.telemetryName != null ? NameFormatter.Capitalize(this.telemetryName) : string.Empty)}Telemetry" : schemaClassName;
        }

        public string FileName { get => $"{this.componentName}Receiver.g.cs"; }

        public string FolderPath { get => this.genNamespace; }
    }
}
