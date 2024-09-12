
namespace Azure.Iot.Operations.ProtocolCompiler
{
    public partial class RustTelemetryReceiver : ITemplateTransform
    {
        private readonly string? telemetryName;
        private readonly string genNamespace;
        private readonly string serializerSubNamespace;
        private readonly string serializerClassName;
        private readonly string schemaClassName;

        public RustTelemetryReceiver(string? telemetryName, string genNamespace, string serializerSubNamespace, string serializerClassName, string schemaClassName)
        {
            this.telemetryName = telemetryName;
            this.genNamespace = genNamespace;
            this.serializerSubNamespace = serializerSubNamespace;
            this.serializerClassName = string.Format(serializerClassName, string.Empty);
            this.schemaClassName = schemaClassName == "" ? "byte[]" : schemaClassName;
        }

        public string FileName { get => NamingSupport.ToSnakeCase($"{this.schemaClassName}Receiver.rs"); }

        public string FolderPath { get => Path.Combine(SubPaths.Rust, this.genNamespace); }
    }
}
