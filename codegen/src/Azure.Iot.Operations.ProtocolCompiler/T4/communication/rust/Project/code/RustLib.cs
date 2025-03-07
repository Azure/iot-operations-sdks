namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Text.RegularExpressions;

    public partial class RustLib : IUpdatingTransform
    {
        private readonly bool generateProject;
        private readonly List<string> modules;

        public RustLib(CodeName genNamespace, CodeName? sharedPrefix, bool generateProject)
        {
            this.generateProject = generateProject;
            this.modules = new List<string> { "common_types", genNamespace.GetFolderName(TargetLanguage.Rust) };
            if (sharedPrefix != null)
            {
                this.modules.Add(sharedPrefix.GetFolderName(TargetLanguage.Rust));
            }
            this.modules.Sort();
        }

        public string FileName { get => this.generateProject ? "lib.rs" : "mod.rs"; }

        public string FolderPath { get => string.Empty; }

        public string FilePattern { get => this.FileName; }

        public bool TryUpdateFile(string filePath)
        {
            string fileText = File.ReadAllText(filePath);
            HashSet<string> extantModules = new(Regex.Matches(fileText, "pub mod ([A-Za-z0-9_]+);").Select(m => m.Groups[1].Captures[0].Value));

            List<string> newModules = this.modules.Where(m => !extantModules.Contains(m)).ToList();
            if (newModules.Count == 0)
            {
                return false;
            }

            File.AppendAllLines(filePath, newModules.Select(m => $"pub mod {m};"));
            return true;
        }
    }
}
