namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.IO;
    using Azure.Iot.Operations.CodeGeneration;

    public partial class RustTelemetrySender : IEnvoyTemplateTransform
    {
        private readonly CodeName telemetryName;
        private readonly CodeName genNamespace;
        private readonly ITypeName schemaType;
        private readonly string topicPattern;
        private readonly CodeName messageName;
        private readonly CodeName componentName;

        public RustTelemetrySender(CodeName telemetryName, CodeName genNamespace, ITypeName schemaType, string topicPattern)
        {
            this.telemetryName = telemetryName;
            this.genNamespace = genNamespace;
            this.schemaType = schemaType;
            this.topicPattern = topicPattern;
            this.messageName = new CodeName(this.telemetryName, "telemetry", "message");
            this.componentName = new CodeName(this.telemetryName, "telemetry", "sender");
        }

        public string FileName { get => $"{this.componentName.GetFileName(TargetLanguage.Rust)}.rs"; }

        public string FolderPath { get => Path.Combine("src", this.genNamespace.GetFolderName(TargetLanguage.Rust)); }
    }
}
