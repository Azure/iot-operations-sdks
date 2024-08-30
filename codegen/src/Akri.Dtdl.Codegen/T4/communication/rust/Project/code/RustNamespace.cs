namespace Akri.Dtdl.Codegen
{
    using System.Text;
    using System.Text.RegularExpressions;

    internal class RustNamespace : ITemplateTransform
    {
        private readonly string genNamespace;
        private readonly HashSet<string> sourceFilePaths;

        public RustNamespace(string genNamespace, HashSet<string> sourceFilePaths)
        {
            this.genNamespace = genNamespace;
            this.sourceFilePaths = sourceFilePaths;
        }

        public string FileName { get => $"{this.genNamespace}.rs"; }

        public string FolderPath { get => SubPaths.Rust; }

        public string TransformText()
        {
            Regex regex = new($"[\\/\\\\]{this.genNamespace}[\\/\\\\][A-Za-z0-9_\\.]+\\.rs", RegexOptions.Compiled);
            StringBuilder stringBuilder = new StringBuilder();

            foreach (var sourceFilePath in sourceFilePaths)
            {
                if (regex.IsMatch(sourceFilePath))
                {
                    stringBuilder.AppendLine($"pub mod {Path.GetFileNameWithoutExtension(sourceFilePath)};");
                }
            }

            return stringBuilder.ToString();
        }
    }
}
