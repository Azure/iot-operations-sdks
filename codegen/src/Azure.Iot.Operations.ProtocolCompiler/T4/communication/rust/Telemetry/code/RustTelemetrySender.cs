
namespace Azure.Iot.Operations.ProtocolCompiler
{
    public partial class RustTelemetrySender : ITemplateTransform
    {
        private readonly CodeName telemetryName;
        private readonly CodeName genNamespace;
        private readonly ITypeName schemaType;
        private readonly CodeName messageName;
        private readonly CodeName componentName;

        public RustTelemetrySender(CodeName telemetryName, CodeName genNamespace, ITypeName schemaType)
        {
            this.telemetryName = telemetryName;
            this.genNamespace = genNamespace;
            this.schemaType = schemaType;
            this.messageName = new CodeName(this.telemetryName, "telemetry", "message");
            this.componentName = new CodeName(this.telemetryName, "telemetry", "sender");
        }

        public string FileName { get => $"{this.componentName.GetFileName(TargetLanguage.Rust)}.rs"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.Rust); }
    }
}
