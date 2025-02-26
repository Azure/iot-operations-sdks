namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System;

    public partial class PythonTelemetryReceiver : ITemplateTransform
    {
        private readonly string? telemetryName;
        private readonly CodeName genNamespace;
        private readonly string serializerSubNamespace;
        private readonly string serializerClassName;
        private readonly string schemaClassName;

        public PythonTelemetryReceiver(string? telemetryName, CodeName genNamespace, string serializerSubNamespace, string serializerClassName, string schemaClassName)
        {
            this.telemetryName = telemetryName;
            this.genNamespace = genNamespace;
            this.serializerSubNamespace = serializerSubNamespace;
            this.serializerClassName = string.Format(serializerClassName, string.Empty);
            this.schemaClassName = schemaClassName == "" ? "any" : schemaClassName;
        }

        public string FileName { get => $"{this.schemaClassName}Receiver_g.py"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.Python); }
    }
}
