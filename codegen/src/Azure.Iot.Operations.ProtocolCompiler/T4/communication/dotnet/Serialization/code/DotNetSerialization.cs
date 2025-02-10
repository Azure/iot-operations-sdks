namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.IO;
    using System.Linq;

    public partial class DotNetSerialization : ITemplateTransform
    {
        private readonly string projectName;
        private readonly CodeName genNamespace;
        private readonly CodeName schemaClassName;
        private readonly string schemaText;

        public DotNetSerialization(string projectName, CodeName genNamespace, CodeName schemaClassName, string? workingPath)
        {
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.schemaClassName = schemaClassName;
            this.schemaText = string.Empty;

            if (workingPath != null)
            {
                string? schemaFile = Directory.GetFiles(Path.Combine(workingPath, this.genNamespace.GetFolderName(TargetLanguage.Independent)), $"{schemaClassName.GetFileName(TargetLanguage.Independent)}.*").FirstOrDefault();
                if (schemaFile != null)
                {
                    this.schemaText = File.ReadAllText(schemaFile).Trim();
                }
            }
        }

        public string FileName { get => $"{this.schemaClassName.GetFileName(TargetLanguage.CSharp, "serialization")}.g.cs"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.CSharp); }
    }
}
