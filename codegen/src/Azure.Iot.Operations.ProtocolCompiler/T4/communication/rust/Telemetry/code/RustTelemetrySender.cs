
namespace Azure.Iot.Operations.ProtocolCompiler
{
    public partial class RustTelemetrySender : ITemplateTransform
    {
        private readonly string? telemetryName;
        private readonly string genNamespace;
        private readonly string schemaClassName;
        private readonly string componentName;

        public RustTelemetrySender(string? telemetryName, string genNamespace, string schemaClassName)
        {
            this.telemetryName = telemetryName;
            this.genNamespace = genNamespace;
            this.schemaClassName = schemaClassName == "" ? "Bytes" : schemaClassName;
            this.componentName = schemaClassName == "" ? $"{(this.telemetryName != null ? NameFormatter.Capitalize(this.telemetryName) : "Bytes")}Telemetry" : schemaClassName;
        }

        public string FileName { get => NamingSupport.ToSnakeCase($"{this.componentName}Sender.rs"); }

        public string FolderPath { get => Path.Combine(SubPaths.Rust, this.genNamespace); }
    }
}
