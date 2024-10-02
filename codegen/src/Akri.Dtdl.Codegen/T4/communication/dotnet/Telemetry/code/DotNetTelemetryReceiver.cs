namespace Akri.Dtdl.Codegen
{
    using System;

    public partial class DotNetTelemetryReceiver : ITemplateTransform
    {
        private readonly string? telemetryName;
        private readonly string projectName;
        private readonly string genNamespace;
        private readonly string serviceName;
        private readonly string serializerSubNamespace;
        private readonly string serializerClassName;
        private readonly string schemaClassName;

        public DotNetTelemetryReceiver(string? telemetryName, string projectName, string genNamespace, string serviceName, string serializerSubNamespace, string serializerClassName, string serializerEmptyType, string schemaClassName)
        {
            this.telemetryName = telemetryName;
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.serviceName = serviceName;
            this.serializerSubNamespace = serializerSubNamespace;
            this.serializerClassName = string.Format(serializerClassName, $"<{schemaClassName}, {serializerEmptyType}>");
            this.schemaClassName = schemaClassName == "" ? "byte[]" : schemaClassName;
        }

        public string FileName { get => $"{this.schemaClassName}Receiver.g.cs"; }

        public string FolderPath { get => this.genNamespace; }
    }
}
