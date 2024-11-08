
namespace Azure.Iot.Operations.ProtocolCompiler
{
    public partial class RustTelemetryReceiver : ITemplateTransform
    {
        private readonly string? telemetryName;
        private readonly string genNamespace;
        private readonly string schemaClassName;
        private readonly bool useSharedSubscription;

        public RustTelemetryReceiver(string? telemetryName, string genNamespace, string schemaClassName, bool useSharedSubscription)
        {
            this.telemetryName = telemetryName;
            this.genNamespace = genNamespace;
            this.schemaClassName = schemaClassName == "" ? "byte[]" : schemaClassName;
            this.useSharedSubscription = useSharedSubscription;
        }

        public string FileName { get => NamingSupport.ToSnakeCase($"{this.schemaClassName}Receiver.rs"); }

        public string FolderPath { get => Path.Combine(SubPaths.Rust, this.genNamespace); }
    }
}
