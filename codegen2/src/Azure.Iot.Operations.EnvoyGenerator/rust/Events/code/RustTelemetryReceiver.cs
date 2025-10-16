namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.IO;
    using Azure.Iot.Operations.CodeGeneration;

    public partial class RustTelemetryReceiver : IEnvoyTemplateTransform
    {
        private readonly CodeName telemetryName;
        private readonly CodeName genNamespace;
        private readonly ITypeName schemaType;
        private readonly CodeName messageName;
        private readonly CodeName componentName;
        private readonly string? serviceGroupId;
        private readonly string topicPattern;

        public RustTelemetryReceiver(CodeName telemetryName, CodeName genNamespace, ITypeName schemaType, string? serviceGroupId, string topicPattern)
        {
            this.telemetryName = telemetryName;
            this.genNamespace = genNamespace;
            this.schemaType = schemaType;
            this.messageName = new CodeName(this.telemetryName, "telemetry", "message");
            this.componentName = new CodeName(this.telemetryName, "telemetry", "receiver");
            this.serviceGroupId = serviceGroupId;
            this.topicPattern = topicPattern;
        }

        public string FileName { get => $"{this.componentName.GetFileName(TargetLanguage.Rust)}.rs"; }

        public string FolderPath { get => Path.Combine("src", this.genNamespace.GetFolderName(TargetLanguage.Rust)); }
    }
}
