
namespace Azure.Iot.Operations.ProtocolCompiler
{
    public partial class GoTelemetrySender : ITemplateTransform
    {
        private readonly string? telemetryName;
        private readonly string genNamespace;
        private readonly string serializerSubNamespace;
        private readonly string schemaClassName;
        private readonly string componentName;

        public GoTelemetrySender(string? telemetryName, string genNamespace, string serializerSubNamespace, string schemaClassName)
        {
            this.telemetryName = telemetryName;
            this.genNamespace = genNamespace;
            this.serializerSubNamespace = serializerSubNamespace;
            this.schemaClassName = schemaClassName;
            this.componentName = schemaClassName == "" ? $"{(this.telemetryName != null ? NameFormatter.Capitalize(this.telemetryName) : string.Empty)}Telemetry" : schemaClassName;
        }

        public string FileName { get => NamingSupport.ToSnakeCase($"{this.componentName}Sender.go"); }

        public string FolderPath { get => this.genNamespace; }
    }
}
