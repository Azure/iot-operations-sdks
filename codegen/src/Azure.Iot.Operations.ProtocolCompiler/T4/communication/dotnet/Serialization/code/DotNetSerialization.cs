namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.IO;
    using System.Linq;

    public partial class DotNetSerialization : ITemplateTransform
    {
        private readonly string projectName;
        private readonly string genNamespace;
        private readonly string schemaClassName;
        private readonly string schemaText;

        public DotNetSerialization(string projectName, string genNamespace, string schemaClassName, string? workingPath)
        {
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.schemaClassName = schemaClassName;
            this.schemaText = string.Empty;

            if (workingPath != null)
            {
                string? schemaFile = Directory.GetFiles(Path.Combine(workingPath, this.genNamespace), $"{schemaClassName}.*").FirstOrDefault();
                if (schemaFile != null)
                {
                    this.schemaText = File.ReadAllText(schemaFile).Trim();
                }
            }
        }

        public string FileName { get => $"{this.schemaClassName}Serialization.g.cs"; }

        public string FolderPath { get => this.genNamespace; }
    }
}
