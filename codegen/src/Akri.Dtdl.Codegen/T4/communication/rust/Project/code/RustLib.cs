namespace Akri.Dtdl.Codegen
{
    public partial class RustLib : ITemplateTransform
    {
        private readonly string genNamespace;

        public RustLib(string genNamespace)
        {
            this.genNamespace = genNamespace;
        }

        public string FileName { get => "lib.rs"; }

        public string FolderPath { get => SubPaths.Rust; }
    }
}
