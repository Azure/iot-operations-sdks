namespace Azure.Iot.Operations.ProtocolCompiler
{
    public partial class RustLib : ITemplateTransform
    {
        private readonly string genRoot;
        private readonly bool generateProject;
        private readonly List<string> modules;

        public RustLib(string genNamespace, string genRoot, bool generateProject)
        {
            this.genRoot = genRoot;
            this.generateProject = generateProject;
            this.modules = new List<string> { "common_types", genNamespace };
            this.modules.Sort();
        }

        public string FileName { get => this.generateProject ? "lib.rs" : "mod.rs"; }

        public string FolderPath { get => string.Empty; }
    }
}
