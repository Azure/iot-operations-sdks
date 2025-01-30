
namespace Azure.Iot.Operations.ProtocolCompiler
{
    public partial class RustTelemetryReceiver : ITemplateTransform
    {
        private readonly CodeName telemetryName;
        private readonly CodeName genNamespace;
        private readonly ITypeName schemaType;
        private readonly CodeName messageName;
        private readonly CodeName componentName;
        private readonly bool useSharedSubscription;

        public RustTelemetryReceiver(CodeName telemetryName, CodeName genNamespace, ITypeName schemaType, bool useSharedSubscription)
        {
            this.telemetryName = telemetryName;
            this.genNamespace = genNamespace;
            this.schemaType = schemaType;
            this.messageName = new CodeName(this.telemetryName, "telemetry", "message");
            this.componentName = new CodeName(this.telemetryName, "telemetry", "receiver");
            this.useSharedSubscription = useSharedSubscription;
        }

        public string FileName { get => $"{this.componentName.GetFileName(TargetLanguage.Rust)}.rs"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.Rust); }
    }
}
