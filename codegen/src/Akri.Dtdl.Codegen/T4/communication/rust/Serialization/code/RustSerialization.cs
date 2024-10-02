namespace Akri.Dtdl.Codegen
{
    using System.IO;
    using System.Linq;

    public partial class RustSerialization : ITemplateTransform
    {
        private static readonly Dictionary<string, List<string>> formatHeaders = new()
        {
            { PayloadFormat.Avro, new List<string> { "use std::io::Cursor;", "use lazy_static;", "use apache_avro;" } },
            { PayloadFormat.Json, new List<string> { "use serde_json;" } },
        };

        private static readonly Dictionary<string, string> formatSchemaCode = new()
        {
            { PayloadFormat.Avro, "lazy_static::lazy_static! { pub static ref SCHEMA: apache_avro::Schema = apache_avro::Schema::parse_str(RAW_SCHEMA).unwrap(); }" },
        };

        private static readonly Dictionary<string, string> formatContentType = new()
        {
            { PayloadFormat.Avro, "application/avro" },
            { PayloadFormat.Cbor, "application/cbor" },
            { PayloadFormat.Json, "application/json" },
            { PayloadFormat.Proto2, "application/protobuf" },
            { PayloadFormat.Proto3, "application/protobuf" },
            { PayloadFormat.Raw, "application/octet-stream" },
        };

        private static readonly Dictionary<string, string> formatFormatIndicator = new()
        {
            { PayloadFormat.Avro, "UnspecifiedBytes" },
            { PayloadFormat.Cbor, "UnspecifiedBytes" },
            { PayloadFormat.Json, "Utf8EncodedCharacterData" },
            { PayloadFormat.Proto2, "UnspecifiedBytes" },
            { PayloadFormat.Proto3, "UnspecifiedBytes" },
            { PayloadFormat.Raw, "UnspecifiedBytes" },
        };

        private static readonly Dictionary<string, List<string>> formatSerializeCode = new()
        {
            { PayloadFormat.Avro, new List<string> {
                "match apache_avro::to_value(self) {",
                "    Ok(v) => { apache_avro::to_avro_datum(&SCHEMA, v).map_err(|e| SerializerError { nested_error: Box::new(e) }) },",
                "    Err(e) => { Err(SerializerError { nested_error: Box::new(e) }) },",
                "}",
            } },
            { PayloadFormat.Json, new List<string> { "serde_json::to_vec(self).map_err(|e| SerializerError { nested_error: Box::new(e) })" } },
        };

        private static readonly Dictionary<string, List<string>> formatDeserializeCode = new()
        {
            { PayloadFormat.Avro, new List<string> {
                "match apache_avro::from_avro_datum(&SCHEMA, &mut Cursor::new(payload), None) {",
                "    Ok(v) => { apache_avro::from_value(&v).map_err(|e| SerializerError { nested_error: Box::new(e) }) },",
                "    Err(e) => { Err(SerializerError { nested_error: Box::new(e) }) },",
                "}",
            } },
            { PayloadFormat.Json, new List<string> { "serde_json::from_slice(payload).map_err(|e| SerializerError { nested_error: Box::new(e) })" } },
        };

        private readonly string genNamespace;
        private readonly string schemaModuleName;
        private readonly string schemaClassName;
        private readonly string schemaText;
        private readonly List<string> headers;
        private readonly string? schemaCode;
        private readonly string? contentType;
        private readonly string? formatIndicator;
        private readonly List<string> serializeCode;
        private readonly List<string> deserializeCode;

        public RustSerialization(string genNamespace, string genFormat, string schemaClassName, string? workingPath)
        {
            this.genNamespace = genNamespace;
            this.schemaModuleName = NamingSupport.ToSnakeCase(schemaClassName);
            this.schemaClassName = schemaClassName;
            this.schemaText = string.Empty;

            this.headers = formatHeaders.GetValueOrDefault(genFormat) ?? new List<string>();
            this.schemaCode = formatSchemaCode.GetValueOrDefault(genFormat);
            this.contentType = formatContentType.GetValueOrDefault(genFormat);
            this.formatIndicator = formatFormatIndicator.GetValueOrDefault(genFormat);
            this.serializeCode = formatSerializeCode.GetValueOrDefault(genFormat) ?? new List<string>();
            this.deserializeCode = formatDeserializeCode.GetValueOrDefault(genFormat) ?? new List<string>();

            if (workingPath != null)
            {
                string? schemaFile = Directory.GetFiles(Path.Combine(workingPath, this.genNamespace), $"{schemaClassName}.*").FirstOrDefault();
                if (schemaFile != null)
                {
                    this.schemaText = File.ReadAllText(schemaFile).Trim();
                }
            }
        }

        public string FileName { get => NamingSupport.ToSnakeCase($"{this.schemaClassName}Serialization.rs"); }

        public string FolderPath { get => Path.Combine(SubPaths.Rust, this.genNamespace); }
    }
}
