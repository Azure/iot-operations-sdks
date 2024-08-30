
namespace Akri.Dtdl.Codegen
{
    public partial class GoTelemetryReceiver : ITemplateTransform
    {
        private readonly string? telemetryName;
        private readonly string genNamespace;
        private readonly string serializerSubNamespace;
        private readonly string schemaClassName;

        public GoTelemetryReceiver(string? telemetryName, string genNamespace, string serializerSubNamespace, string schemaClassName)
        {
            this.telemetryName = telemetryName;
            this.genNamespace = genNamespace;
            this.serializerSubNamespace = serializerSubNamespace;
            this.schemaClassName = schemaClassName == "" ? "byte[]" : schemaClassName;
        }

        public string FileName { get => NamingSupport.ToSnakeCase($"{this.schemaClassName}Receiver.go"); }

        public string FolderPath { get => this.genNamespace; }
    }
}
