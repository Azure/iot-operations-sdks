namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System;

    public partial class JavaTelemetrySender : ITemplateTransform
    {
        private readonly string? telemetryName;
        private readonly CodeName genNamespace;
        private readonly string serializerSubNamespace;
        private readonly string serializerClassName;
        private readonly string schemaClassName;

        public JavaTelemetrySender(string? telemetryName, CodeName genNamespace, string serializerSubNamespace, string serializerClassName, string schemaClassName)
        {
            this.telemetryName = telemetryName;
            this.genNamespace = genNamespace;
            this.serializerSubNamespace = char.ToUpperInvariant(serializerSubNamespace[0]) + serializerSubNamespace.Substring(1);
            this.serializerClassName = serializerClassName;
            this.schemaClassName = schemaClassName;
        }

        public string FileName { get => $"{this.schemaClassName}Sender.java"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.Java); }
    }
}
