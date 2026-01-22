namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.Collections.Generic;
    using System.IO;
    using Azure.Iot.Operations.CodeGeneration;

    public partial class RustSerialization : IEnvoyTemplateTransform
    {
        private static readonly Dictionary<SerializationFormat, string> serdeLibs = new()
        {
            { SerializationFormat.Json, "serde_json" },
        };

        private static readonly Dictionary<SerializationFormat, List<string>> formatStdHeaders = new()
        {
            { SerializationFormat.Json, new List<string> { } },
        };

        private static readonly Dictionary<SerializationFormat, List<string>> formatExtHeaders = new()
        {
            { SerializationFormat.Json, new List<string> { "use serde_json;" } },
        };

        private static readonly Dictionary<SerializationFormat, string> formatContentType = new()
        {
            { SerializationFormat.Json, "application/json" },
        };

        private static readonly Dictionary<SerializationFormat, string> formatFormatIndicator = new()
        {
            { SerializationFormat.Json, "Utf8EncodedCharacterData" },
        };

        private static readonly Dictionary<SerializationFormat, List<string>> formatSerializeCode = new()
        {
            { SerializationFormat.Json, new List<string> { "serde_json::to_vec(&self)" } },
        };

        private static readonly Dictionary<SerializationFormat, List<string>> formatDeserializeCode = new()
        {
            { SerializationFormat.Json, new List<string> { "serde_json::from_slice(payload)" } },
        };

        private readonly CodeName genNamespace;
        private readonly CodeName schemaClassName;
        private readonly string? serdeLib;
        private readonly List<string> stdHeaders;
        private readonly List<string> extHeaders;
        private readonly string? contentType;
        private readonly string? formatIndicator;
        private readonly List<string> serializeCode;
        private readonly List<string> deserializeCode;
        private readonly string srcSubdir;

        public RustSerialization(CodeName genNamespace, SerializationFormat genFormat, CodeName schemaClassName, string srcSubdir)
        {
            this.genNamespace = genNamespace;
            this.schemaClassName = schemaClassName;
            this.srcSubdir = srcSubdir;

            this.serdeLib = serdeLibs.GetValueOrDefault(genFormat);
            this.stdHeaders = formatStdHeaders.GetValueOrDefault(genFormat) ?? new List<string>();
            this.extHeaders = formatExtHeaders.GetValueOrDefault(genFormat) ?? new List<string>();
            this.contentType = formatContentType.GetValueOrDefault(genFormat);
            this.formatIndicator = formatFormatIndicator.GetValueOrDefault(genFormat);
            this.serializeCode = formatSerializeCode.GetValueOrDefault(genFormat) ?? new List<string>();
            this.deserializeCode = formatDeserializeCode.GetValueOrDefault(genFormat) ?? new List<string>();
        }

        public string FileName { get => $"{this.schemaClassName.GetFileName(TargetLanguage.Rust, "serialization")}.rs"; }

        public string FolderPath { get => Path.Combine(this.srcSubdir, this.genNamespace.GetFolderName(TargetLanguage.Rust)); }

        public EndpointTarget EndpointTarget { get => EndpointTarget.Hidden; }
    }
}
