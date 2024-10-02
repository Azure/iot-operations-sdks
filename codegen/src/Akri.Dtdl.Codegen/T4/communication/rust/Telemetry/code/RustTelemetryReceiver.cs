
namespace Akri.Dtdl.Codegen
{
    public partial class RustTelemetryReceiver : ITemplateTransform
    {
        private readonly string? telemetryName;
        private readonly string genNamespace;
        private readonly string schemaClassName;

        public RustTelemetryReceiver(string? telemetryName, string genNamespace, string schemaClassName)
        {
            this.telemetryName = telemetryName;
            this.genNamespace = genNamespace;
            this.schemaClassName = schemaClassName == "" ? "byte[]" : schemaClassName;
        }

        public string FileName { get => NamingSupport.ToSnakeCase($"{this.schemaClassName}Receiver.rs"); }

        public string FolderPath { get => Path.Combine(SubPaths.Rust, this.genNamespace); }
    }
}
