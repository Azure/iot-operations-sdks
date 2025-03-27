
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
        private readonly List<CommandEnvoyInfo> cmdEnvoyInfos;
        private readonly List<TelemetryEnvoyInfo> telemEnvoyInfos;
        private readonly bool doesCommandTargetService;
        private readonly bool doesTelemetryTargetService;
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
            List<CommandEnvoyInfo> cmdEnvoyInfos,
            List<TelemetryEnvoyInfo> telemEnvoyInfos,
            bool doesCommandTargetService,
            bool doesTelemetryTargetService,
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
            this.cmdEnvoyInfos = cmdEnvoyInfos;
            this.telemEnvoyInfos = telemEnvoyInfos;
            this.doesCommandTargetService = doesCommandTargetService;
            this.doesTelemetryTargetService = doesTelemetryTargetService;
            this.generateClient = generateClient;
            this.generateServer = generateServer;
            this.separateTelemetries = separateTelemetries;
        }

        public string FileName { get => "wrapper.go"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.Go); }
    }
}
