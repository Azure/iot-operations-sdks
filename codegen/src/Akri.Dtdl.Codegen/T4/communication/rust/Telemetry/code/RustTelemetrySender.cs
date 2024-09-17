
namespace Akri.Dtdl.Codegen
{
    public partial class RustTelemetrySender : ITemplateTransform
    {
        private readonly string? telemetryName;
        private readonly string genNamespace;
        private readonly string schemaClassName;

        public RustTelemetrySender(string? telemetryName, string genNamespace, string schemaClassName)
        {
            this.telemetryName = telemetryName;
            this.genNamespace = genNamespace;
            this.schemaClassName = schemaClassName == "" ? "byte[]" : schemaClassName;
        }

        public string FileName { get => NamingSupport.ToSnakeCase($"{this.schemaClassName}Sender.rs"); }

        public string FolderPath { get => Path.Combine(SubPaths.Rust, this.genNamespace); }
    }
}
