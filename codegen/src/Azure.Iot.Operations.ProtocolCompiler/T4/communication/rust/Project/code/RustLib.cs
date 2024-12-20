namespace Azure.Iot.Operations.ProtocolCompiler
{
    public partial class RustLib : ITemplateTransform
    {
        private readonly string genRoot;
        private readonly bool genOrUpdateProj;
        private readonly List<string> modules;

        public RustLib(string genNamespace, string genRoot, bool genOrUpdateProj)
        {
            this.genRoot = genRoot;
            this.genOrUpdateProj = genOrUpdateProj;
            this.modules = new List<string> { "common_types", genNamespace };
            this.modules.Sort();
        }

        public string FileName { get => this.genOrUpdateProj ? "lib.rs" : $"{this.genRoot}.rs"; }

        public string FolderPath { get => this.genOrUpdateProj ? string.Empty : ".."; }
    }
}
