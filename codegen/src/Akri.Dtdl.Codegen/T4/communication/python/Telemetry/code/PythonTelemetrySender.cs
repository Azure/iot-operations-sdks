namespace Akri.Dtdl.Codegen
{
    using System;

    public partial class PythonTelemetrySender : ITemplateTransform
    {
        private readonly string? telemetryName;
        private readonly string genNamespace;
        private readonly string serializerSubNamespace;
        private readonly string serializerClassName;
        private readonly string schemaClassName;

        public PythonTelemetrySender(string? telemetryName, string genNamespace, string serializerSubNamespace, string serializerClassName, string schemaClassName)
        {
            this.telemetryName = telemetryName;
            this.genNamespace = genNamespace;
            this.serializerSubNamespace = serializerSubNamespace;
            this.serializerClassName = string.Format(serializerClassName, string.Empty);
            this.schemaClassName = schemaClassName == "" ? "any" : schemaClassName;
        }

        public string FileName { get => $"{this.schemaClassName}Sender_g.py"; }

        public string FolderPath { get => this.genNamespace; }
    }
}
