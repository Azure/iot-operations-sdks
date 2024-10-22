namespace Azure.Iot.Operations.ProtocolCompiler
{
    public partial class RustLib : ITemplateTransform
    {
        private readonly List<string> modules;

        public RustLib(string genNamespace)
        {
            this.modules = new List<string> { "common_types", genNamespace };
            this.modules.Sort();
        }

        public string FileName { get => "lib.rs"; }

        public string FolderPath { get => SubPaths.Rust; }
    }
}
