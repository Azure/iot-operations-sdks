
namespace Azure.Iot.Operations.ProtocolCompiler
{
    public partial class GoService : ITemplateTransform
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
        private readonly bool doesCommandTargetService;
        private readonly bool doesTelemetryTargetService;
        private readonly bool syncApi;

        public GoService(
            string genNamespace,
            string modelId,
            string serviceName,
            string? commandTopic,
            string? telemetryTopic,
            string? cmdServiceGroupId,
            string? telemServiceGroupId,
            List<(string, string?, string?)> cmdNameReqResps,
            List<string> telemSchemas,
            bool doesCommandTargetService,
            bool doesTelemetryTargetService,
            bool syncApi)
        {
            this.genNamespace = genNamespace;
            this.modelId = modelId;
            this.serviceName = serviceName;
            this.commandTopic = commandTopic;
            this.telemetryTopic = telemetryTopic;
            this.cmdServiceGroupId = cmdServiceGroupId;
            this.telemServiceGroupId = telemServiceGroupId;
            this.cmdNameReqResps = cmdNameReqResps;
            this.telemSchemas = telemSchemas;
            this.doesCommandTargetService = doesCommandTargetService;
            this.doesTelemetryTargetService = doesTelemetryTargetService;
            this.syncApi = syncApi;
        }

        public string FileName { get => "wrapper.go"; }

        public string FolderPath { get => this.genNamespace; }
    }
}
