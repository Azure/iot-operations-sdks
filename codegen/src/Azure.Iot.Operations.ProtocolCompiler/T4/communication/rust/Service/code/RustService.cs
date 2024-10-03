namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Text.RegularExpressions;

    public partial class RustService : ITemplateTransform
    {
        private readonly string genNamespace;
        private readonly string modelId;
        private readonly string serviceName;
        private readonly string? commandTopic;
        private readonly string? telemetryTopic;
        private readonly List<(string, string?, string?)> cmdNameReqResps;
        private readonly List<string> telemSchemas;
        private readonly bool doesCommandTargetExecutor;
        private readonly bool doesCommandTargetService;
        private readonly bool doesTelemetryTargetService;
        private readonly HashSet<string> sourceFilePaths;
        private readonly Regex sourceFileRegex;

        public RustService(
            string genNamespace,
            string modelId,
            string serviceName,
            string? commandTopic,
            string? telemetryTopic,
            List<(string, string?, string?)> cmdNameReqResps,
            List<string> telemSchemas,
            bool doesCommandTargetExecutor,
            bool doesCommandTargetService,
            bool doesTelemetryTargetService,
            HashSet<string> sourceFilePaths)
        {
            this.genNamespace = genNamespace;
            this.modelId = modelId;
            this.serviceName = serviceName;
            this.commandTopic = commandTopic;
            this.telemetryTopic = telemetryTopic;
            this.cmdNameReqResps = cmdNameReqResps;
            this.telemSchemas = telemSchemas;
            this.doesCommandTargetExecutor = doesCommandTargetExecutor;
            this.doesCommandTargetService = doesCommandTargetService;
            this.doesTelemetryTargetService = doesTelemetryTargetService;
            this.sourceFilePaths = sourceFilePaths;
            this.sourceFileRegex = new($"[\\/\\\\]{genNamespace}[\\/\\\\][A-Za-z0-9_\\.]+\\.rs");
        }

        public string FileName { get => $"{this.genNamespace}.rs"; }

        public string FolderPath { get => SubPaths.Rust; }
    }
}
