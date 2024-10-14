
namespace Azure.Iot.Operations.ProtocolCompiler
{
    public partial class JavaService : ITemplateTransform
    {
        private readonly string genNamespace;
        private readonly string modelId;
        private readonly string serviceName;
        private readonly string? commandTopic;
        private readonly string? telemetryTopic;
        private readonly string? serviceGroupId;
        private readonly List<(string, string?, string?)> cmdNameReqResps;
        private readonly List<string> telemSchemas;
        private readonly bool doesCommandTargetExecutor;
        private readonly bool doesCommandTargetService;
        private readonly bool doesTelemetryTargetService;

        public JavaService(
            string genNamespace,
            string modelId,
            string serviceName,
            string? commandTopic,
            string? telemetryTopic,
            string? serviceGroupId,
            List<(string, string?, string?)> cmdNameReqResps,
            List<string> telemSchemas,
            bool doesCommandTargetExecutor,
            bool doesCommandTargetService,
            bool doesTelemetryTargetService)
        {
            this.genNamespace = genNamespace;
            this.modelId = modelId;
            this.serviceName = serviceName;
            this.commandTopic = commandTopic;
            this.telemetryTopic = telemetryTopic;
            this.serviceGroupId = serviceGroupId;
            this.cmdNameReqResps = cmdNameReqResps;
            this.telemSchemas = telemSchemas;
            this.doesCommandTargetExecutor = doesCommandTargetExecutor;
            this.doesCommandTargetService = doesCommandTargetService;
            this.doesTelemetryTargetService = doesTelemetryTargetService;
        }

        public string FileName { get => $"{this.serviceName}.java"; }

        public string FolderPath { get => this.genNamespace; }
    }
}
