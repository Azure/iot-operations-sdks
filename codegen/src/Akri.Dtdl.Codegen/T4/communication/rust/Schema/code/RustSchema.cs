namespace Akri.Dtdl.Codegen
{
    using System.IO;
    using System.Linq;

    public partial class RustSchema : ITemplateTransform
    {
        private readonly string genNamespace;
        private readonly string schemaModuleName;
        private readonly string schemaClassName;
        private readonly string schemaText;

        public RustSchema(string genNamespace, string schemaClassName, string? workingPath)
        {
            this.genNamespace = genNamespace;
            this.schemaModuleName = NamingSupport.ToSnakeCase(schemaClassName);
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

        public string FileName { get => NamingSupport.ToSnakeCase($"{this.schemaClassName}Schema.rs"); }

        public string FolderPath { get => Path.Combine(SubPaths.Rust, this.genNamespace); }
    }
}
