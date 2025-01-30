
namespace Azure.Iot.Operations.ProtocolCompiler
{
    public partial class GoTelemetryReceiver : ITemplateTransform
    {
        private readonly CodeName telemetryName;
        private readonly CodeName genNamespace;
        private readonly string serializerSubNamespace;
        private readonly ITypeName schemaType;
        private readonly CodeName componentName;

        public GoTelemetryReceiver(CodeName telemetryName, CodeName genNamespace, string serializerSubNamespace, ITypeName schemaType)
        {
            this.telemetryName = telemetryName;
            this.genNamespace = genNamespace;
            this.serializerSubNamespace = serializerSubNamespace;
            this.schemaType = schemaType;
            this.componentName = new CodeName(this.telemetryName, "telemetry", "receiver");
        }

        public string FileName { get => $"{this.componentName.GetFileName(TargetLanguage.Go)}.go"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.Go); }
    }
}
