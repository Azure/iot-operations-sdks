
namespace Azure.Iot.Operations.ProtocolCompiler
{
    public partial class GoService : ITemplateTransform
    {
        private readonly CodeName genNamespace;
        private readonly string modelId;
        private readonly CodeName serviceName;
        private readonly string? commandTopic;
        private readonly string? telemetryTopic;
        private readonly string? cmdServiceGroupId;
        private readonly string? telemServiceGroupId;
        private readonly List<(CodeName, ITypeName?, ITypeName?)> cmdNameReqResps;
        private readonly List<(CodeName, ITypeName)> telemNameSchemas;
        private readonly bool doesCommandTargetService;
        private readonly bool doesTelemetryTargetService;
        private readonly bool syncApi;
        private readonly bool generateClient;
        private readonly bool generateServer;
        private readonly bool separateTelemetries;

        public GoService(
            CodeName genNamespace,
            string modelId,
            CodeName serviceName,
            string? commandTopic,
            string? telemetryTopic,
            string? cmdServiceGroupId,
            string? telemServiceGroupId,
            List<(CodeName, ITypeName?, ITypeName?)> cmdNameReqResps,
            List<(CodeName, ITypeName)> telemNameSchemas,
            bool doesCommandTargetService,
            bool doesTelemetryTargetService,
            bool syncApi,
            bool generateClient,
            bool generateServer,
            bool separateTelemetries)
        {
            this.genNamespace = genNamespace;
            this.modelId = modelId;
            this.serviceName = serviceName;
            this.commandTopic = commandTopic;
            this.telemetryTopic = telemetryTopic;
            this.cmdServiceGroupId = cmdServiceGroupId;
            this.telemServiceGroupId = telemServiceGroupId;
            this.cmdNameReqResps = cmdNameReqResps;
            this.telemNameSchemas = telemNameSchemas;
            this.doesCommandTargetService = doesCommandTargetService;
            this.doesTelemetryTargetService = doesTelemetryTargetService;
            this.syncApi = syncApi;
            this.generateClient = generateClient;
            this.generateServer = generateServer;
            this.separateTelemetries = separateTelemetries;
        }

        public string FileName { get => "wrapper.go"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.Go); }
    }
}
