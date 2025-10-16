namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.Collections.Generic;
    using System.IO;
    using Azure.Iot.Operations.CodeGeneration;

    public partial class RustLib : IEnvoyTemplateTransform
    {
        private readonly bool generateProject;
        private readonly List<string> modules;

        public RustLib(CodeName genNamespace, bool generateProject)
        {
            this.generateProject = generateProject;
            this.modules = new List<string> { "common_types", genNamespace.GetFolderName(TargetLanguage.Rust) };
            this.modules.Sort();
        }

        public string FileName { get => this.generateProject ? "lib.rs" : "mod.rs"; }

        public string FolderPath { get => "src"; }
    }
}
