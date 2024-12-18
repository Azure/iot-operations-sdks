namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Linq;
    using System.Text.RegularExpressions;

    public partial class RustService : ITemplateTransform
    {
        private readonly string genNamespace;
        private readonly string modelId;
        private readonly string serviceName;
        private readonly string? commandTopic;
        private readonly string? telemetryTopic;
        private readonly string? cmdServiceGroupId;
        private readonly string? telemServiceGroupId;
        private readonly List<(string, string?, string?)> cmdNameReqResps;
        private readonly List<string> telemSchemas;
        private readonly bool doesCommandTargetExecutor;
        private readonly bool doesCommandTargetService;
        private readonly bool doesTelemetryTargetService;
        private readonly bool generateClient;
        private readonly bool generateServer;
        private readonly Regex sourceFileRegex;
        private readonly List<string> modules;

        public RustService(
            string genNamespace,
            string modelId,
            string serviceName,
            string? commandTopic,
            string? telemetryTopic,
            string? cmdServiceGroupId,
            string? telemServiceGroupId,
            List<(string, string?, string?)> cmdNameReqResps,
            List<(string?, string)> telemNameSchemas,
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
            this.sourceFileRegex = new($"[\\/\\\\]{genNamespace}[\\/\\\\][A-Za-z0-9_\\.]+\\.rs");
            this.modules = sourceFilePaths.Where(p => this.sourceFileRegex.IsMatch(p)).Select(p => Path.GetFileNameWithoutExtension(p)).Order().ToList();
        }

        public string FileName { get => $"{this.genNamespace}.rs"; }

        public string FolderPath { get => SubPaths.Rust; }
    }
}
