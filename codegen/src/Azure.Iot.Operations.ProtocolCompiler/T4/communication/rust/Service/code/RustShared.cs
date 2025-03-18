namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Linq;
    using System.Text.RegularExpressions;

    public partial class RustShared : ITemplateTransform
    {
        private readonly CodeName sharedNamespace;
        private readonly List<string> modules;

        public RustShared(CodeName sharedNamespace, string genRoot)
        {
            this.sharedNamespace = sharedNamespace;
            string sharedDirPath = Path.Combine(genRoot, sharedNamespace.GetFolderName(TargetLanguage.Rust));
            this.modules = Directory.GetFiles(sharedDirPath, $"*.rs").Select(p => Path.GetFileNameWithoutExtension(p)).Order().ToList();
        }

        public string FileName { get => $"{this.sharedNamespace.GetFolderName(TargetLanguage.Rust)}.rs"; }

        public string FolderPath { get => string.Empty; }
    }
}
