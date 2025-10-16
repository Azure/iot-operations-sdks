namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.Collections.Generic;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;

    public partial class RustCargoToml : IEnvoyTemplateTransform
    {
        internal static readonly Dictionary<SerializationFormat, List<(string, string)>> serializerPackageVersions = new()
        {
            { SerializationFormat.Json, new List<(string, string)> { ("serde_json", "1.0.105") } },
        };

        private readonly bool generateProject;
        private readonly string projectName;
        private readonly string? sdkPath;
        private readonly List<(string, string)> packageVersions;
        private readonly string srcSubdir;

        public RustCargoToml(string projectName, List<SerializationFormat> genFormats, string? sdkPath, bool generateProject, string srcSubdir)
        {
            this.generateProject = generateProject;
            this.projectName = projectName;
            this.sdkPath = sdkPath?.Replace('\\', '/');
            this.srcSubdir = srcSubdir;
            packageVersions = genFormats.Select(f => serializerPackageVersions[f]).SelectMany(pv => pv).Distinct().ToList();
        }

        public string FileName { get => generateProject ? "Cargo.toml" : "dependencies.md"; }

        public string FolderPath { get => this.generateProject ? string.Empty : this.srcSubdir; }
    }
}
