
namespace Azure.Iot.Operations.ProtocolCompiler
{
    public partial class RustTelemetryReceiver : ITemplateTransform
    {
        private readonly string? telemetryName;
        private readonly string genNamespace;
        private readonly string schemaClassName;
        private readonly string componentName;
        private readonly bool useSharedSubscription;

        public RustTelemetryReceiver(string? telemetryName, string genNamespace, string schemaClassName, bool useSharedSubscription)
        {
            this.telemetryName = telemetryName;
            this.genNamespace = genNamespace;
            this.schemaClassName = schemaClassName == "" ? "Bytes" : schemaClassName;
            this.componentName = schemaClassName == "" ? $"{(this.telemetryName != null ? NameFormatter.Capitalize(this.telemetryName) : "Bytes")}Telemetry" : schemaClassName;
            this.useSharedSubscription = useSharedSubscription;
        }

        public string FileName { get => NamingSupport.ToSnakeCase($"{this.componentName}Receiver.rs"); }

        public string FolderPath { get => this.genNamespace; }
    }
}
