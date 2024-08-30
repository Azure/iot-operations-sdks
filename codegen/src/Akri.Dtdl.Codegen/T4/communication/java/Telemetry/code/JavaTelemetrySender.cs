namespace Akri.Dtdl.Codegen
{
    using System;

    public partial class JavaTelemetrySender : ITemplateTransform
    {
        private readonly string? telemetryName;
        private readonly string genNamespace;
        private readonly string serializerSubNamespace;
        private readonly string serializerClassName;
        private readonly string schemaClassName;

        public JavaTelemetrySender(string? telemetryName, string genNamespace, string serializerSubNamespace, string serializerClassName, string schemaClassName)
        {
            this.telemetryName = telemetryName;
            this.genNamespace = genNamespace;
            this.serializerSubNamespace = char.ToUpperInvariant(serializerSubNamespace[0]) + serializerSubNamespace.Substring(1);
            this.serializerClassName = serializerClassName;
            this.schemaClassName = schemaClassName;
        }

        public string FileName { get => $"{this.schemaClassName}Sender.java"; }

        public string FolderPath { get => this.genNamespace; }
    }
}
