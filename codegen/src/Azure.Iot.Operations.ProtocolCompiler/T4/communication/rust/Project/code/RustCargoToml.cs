
namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Text;

    public partial class RustCargoToml : ITemplateTransform
    {
        internal static readonly Dictionary<string, List<(string, string)>> serializerPackageVersions = new()
        {
            { PayloadFormat.Avro, new List<(string, string)> { ("apache-avro", "0.16.0"), ("lazy_static", "1.4.0") } },
            { PayloadFormat.Cbor, new List<(string, string)> { } },
            { PayloadFormat.Json, new List<(string, string)> { ("serde_json", "1.0.105") } },
            { PayloadFormat.Proto2, new List<(string, string)> { } },
            { PayloadFormat.Proto3, new List<(string, string)> { } },
            { PayloadFormat.Raw, new List<(string, string)> { } },
        };

        private readonly string genRoot;
        private readonly bool generateProject;
        private readonly string projectName;
        private readonly string? sdkPath;
        private readonly bool usesAnySchemas;
        private readonly bool usesIntEnum;
        private readonly List<(string, string)> packageVersions;

        public RustCargoToml(string projectName, string genFormat, string? sdkPath, HashSet<SchemaKind> distinctSchemaKinds, string genRoot, bool generateProject)
        {
            this.genRoot = genRoot;
            this.generateProject = generateProject;
            this.projectName = projectName;
            this.sdkPath = sdkPath?.Replace('\\', '/');
            this.usesAnySchemas = distinctSchemaKinds.Any();
            this.usesIntEnum = distinctSchemaKinds.Contains(SchemaKind.EnumInt);

            packageVersions = serializerPackageVersions[genFormat];
        }

        public string FileName { get => generateProject ? "Cargo.toml" : "dependencies.md"; }

        public string FolderPath { get => this.generateProject ? ".." : string.Empty; }
    }
}
