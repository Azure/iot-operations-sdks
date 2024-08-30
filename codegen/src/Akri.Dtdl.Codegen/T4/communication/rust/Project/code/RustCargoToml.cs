
namespace Akri.Dtdl.Codegen
{
    using System.Text;

    public partial class RustCargoToml : ITemplateTransform
    {
        internal static readonly Dictionary<string, List<(string, string)>> serializerPackageVersions = new()
        {
            { PayloadFormat.Avro, new List<(string, string)> { ("apache-avro", "0.16.0") } },
            { PayloadFormat.Cbor, new List<(string, string)> { } },
            { PayloadFormat.Json, new List<(string, string)> { ("serde_json", "1.0.105") } },
            { PayloadFormat.Proto2, new List<(string, string)> { } },
            { PayloadFormat.Proto3, new List<(string, string)> { } },
            { PayloadFormat.Raw, new List<(string, string)> { } },
        };

        private readonly string projectName;
        private readonly string sdkPath;
        private readonly List<(string, string)> packageVersions;

        public RustCargoToml(string projectName, string genFormat, string? sdkPath)
        {
            this.projectName = NamingSupport.ToSnakeCase(projectName);
            this.sdkPath = sdkPath?.Replace('\\', '/') ?? "sdk";

            packageVersions = serializerPackageVersions[genFormat];
        }

        public string FileName { get => "Cargo.toml"; }

        public string FolderPath { get => string.Empty; }
    }
}
