namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Linq;
    using System.Text.RegularExpressions;

    public partial class RustService : ITemplateTransform
    {
        private readonly CodeName genNamespace;
        private readonly string modelId;
        private readonly CodeName serviceName;
        private readonly string? commandTopic;
        private readonly string? telemetryTopic;
        private readonly string? cmdServiceGroupId;
        private readonly string? telemServiceGroupId;
        private readonly List<(CodeName, ITypeName?, ITypeName?)> cmdNameReqResps;
        private readonly List<ITypeName> telemSchemas;
        private readonly bool doesCommandTargetExecutor;
        private readonly bool doesCommandTargetService;
        private readonly bool doesTelemetryTargetService;
        private readonly bool generateClient;
        private readonly bool generateServer;
        private readonly Regex sourceFileRegex;
        private readonly List<string> modules;

        public RustService(
            CodeName genNamespace,
            string modelId,
            CodeName serviceName,
            string? commandTopic,
            string? telemetryTopic,
            string? cmdServiceGroupId,
            string? telemServiceGroupId,
            List<(CodeName, ITypeName?, ITypeName?)> cmdNameReqResps,
            List<(CodeName, ITypeName)> telemNameSchemas,
            bool doesCommandTargetExecutor,
            bool doesCommandTargetService,
            bool doesTelemetryTargetService,
            bool generateClient,
            bool generateServer,
            HashSet<string> sourceFilePaths)
        {
            this.genNamespace = genNamespace;
            this.modelId = modelId;
            this.serviceName = serviceName;
            this.commandTopic = commandTopic;
            this.cmdServiceGroupId = cmdServiceGroupId;
            this.telemServiceGroupId = telemServiceGroupId;
            this.telemetryTopic = telemetryTopic;
            this.cmdNameReqResps = cmdNameReqResps;
            this.telemSchemas = telemNameSchemas.Select(tns => tns.Item2).ToList();
            this.doesCommandTargetExecutor = doesCommandTargetExecutor;
            this.doesCommandTargetService = doesCommandTargetService;
            this.doesTelemetryTargetService = doesTelemetryTargetService;
            this.generateClient = generateClient;
            this.generateServer = generateServer;
            this.sourceFileRegex = new($"[\\/\\\\]{genNamespace.GetFolderName(TargetLanguage.Rust)}[\\/\\\\][A-Za-z0-9_\\.]+\\.rs");
            this.modules = sourceFilePaths.Where(p => this.sourceFileRegex.IsMatch(p)).Select(p => Path.GetFileNameWithoutExtension(p)).Order().ToList();
        }

        public string FileName { get => $"{this.genNamespace.GetFolderName(TargetLanguage.Rust)}.rs"; }

        public string FolderPath { get => string.Empty; }
    }
}
