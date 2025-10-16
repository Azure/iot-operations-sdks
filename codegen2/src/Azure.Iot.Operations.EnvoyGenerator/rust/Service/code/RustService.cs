namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;

    public partial class RustService : IEnvoyTemplateTransform
    {
        private readonly CodeName genNamespace;
        private readonly string modelId;
        private readonly bool generateClient;
        private readonly bool generateServer;
        private readonly List<string> modules;

        public RustService(
            CodeName genNamespace,
            string modelId,
            List<string> envoyFilenames,
            bool generateClient,
            bool generateServer)
        {
            this.genNamespace = genNamespace;
            this.modelId = modelId;
            this.generateClient = generateClient;
            this.generateServer = generateServer;
            this.modules = envoyFilenames.Select(f => Path.GetFileNameWithoutExtension(f)).Order().ToList();
        }

        public string FileName { get => $"{this.genNamespace.GetFolderName(TargetLanguage.Rust)}.rs"; }

        public string FolderPath { get => "src"; }
    }
}
