namespace Akri.Dtdl.Codegen
{
    public partial class RustSchemata : ITemplateTransform
    {
        private static readonly Dictionary<string, SerializerValues> serializerValueMap = new()
        {
            { PayloadFormat.Avro, new SerializerValues("apache_avro", "Schema", "Schema::parse_str({0}::get_schema()).unwrap()") },
        };

        private readonly string genNamespace;
        private readonly List<string> schemaTypes;
        private readonly SerializerValues serializerValues;

        public static bool TryCreate(string genNamespace, string genFormat, List<string> schemaTypes, out RustSchemata? rustSchemata)
        {
            if (serializerValueMap.TryGetValue(genFormat, out SerializerValues serializerValues))
            {
                rustSchemata = new RustSchemata(genNamespace, schemaTypes, serializerValues);
                return true;
            }
            else
            {
                rustSchemata = null;
                return false;
            }
        }

        private RustSchemata(string genNamespace, List<string> schemaTypes, SerializerValues serializerValues)
        {
            this.genNamespace = genNamespace;
            this.schemaTypes = schemaTypes;
            this.serializerValues = serializerValues;
        }

        public string FileName { get => "schemata.rs"; }

        public string FolderPath { get => Path.Combine(SubPaths.Rust, "serialization"); }

        private readonly struct SerializerValues
        {
            public SerializerValues(string serializerNamespace, string schemaTypeName, string parser)
            {
                SerializerNamespace = serializerNamespace;
                SchemaTypeName = schemaTypeName;
                Parser = parser;
            }

            public readonly string SerializerNamespace;
            public readonly string SchemaTypeName;
            public readonly string Parser;
        }
    }
}
