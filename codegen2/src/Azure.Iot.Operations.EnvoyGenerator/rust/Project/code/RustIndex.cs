namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;

    public partial class RustIndex : IEnvoyTemplateTransform
    {
        private readonly CodeName genNamespace;
        private readonly bool generateClient;
        private readonly bool generateServer;
        private readonly List<string> modules;
        private readonly string srcSubdir;

        public RustIndex(
            CodeName genNamespace,
            List<string> envoyFilenames,
            bool generateClient,
            bool generateServer,
            string srcSubdir)
        {
            this.genNamespace = genNamespace;
            this.generateClient = generateClient;
            this.generateServer = generateServer;
            this.srcSubdir = srcSubdir;
            this.modules = envoyFilenames.Select(f => Path.GetFileNameWithoutExtension(f)).Order().ToList();
        }

        public string FileName { get => $"{this.genNamespace.GetFolderName(TargetLanguage.Rust)}.rs"; }

        public string FolderPath { get => this.srcSubdir; }
    }
}
