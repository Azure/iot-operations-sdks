namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Linq;
    using System.Text.RegularExpressions;

    public partial class RustService : ITemplateTransform
    {
        private readonly CodeName genNamespace;
        private readonly string modelId;
        private readonly string? commandTopic;
        private readonly string? telemetryTopic;
        private readonly string? cmdServiceGroupId;
        private readonly string? telemServiceGroupId;
        private readonly bool generateClient;
        private readonly bool generateServer;
        private readonly List<string> modules;

        public RustService(
            CodeName genNamespace,
            string modelId,
            string? commandTopic,
            string? telemetryTopic,
            string? cmdServiceGroupId,
            string? telemServiceGroupId,
            bool generateClient,
            bool generateServer,
            string genRoot)
        {
            this.genNamespace = genNamespace;
            this.modelId = modelId;
            this.commandTopic = commandTopic;
            this.telemetryTopic = telemetryTopic;
            this.cmdServiceGroupId = cmdServiceGroupId;
            this.telemServiceGroupId = telemServiceGroupId;
            this.generateClient = generateClient;
            this.generateServer = generateServer;
            string envoyDirPath = Path.Combine(genRoot, this.genNamespace.GetFolderName(TargetLanguage.Rust));
            this.modules = Directory.GetFiles(envoyDirPath, $"*.rs").Select(p => Path.GetFileNameWithoutExtension(p)).Order().ToList();
        }

        public string FileName { get => $"{this.genNamespace.GetFolderName(TargetLanguage.Rust)}.rs"; }

        public string FolderPath { get => string.Empty; }
    }
}
