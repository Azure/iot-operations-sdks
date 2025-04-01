
namespace Azure.Iot.Operations.ProtocolCompiler
{
    public partial class DotNetService : ITemplateTransform
    {
        private readonly string projectName;
        private readonly CodeName genNamespace;
        private readonly CodeName? sharedNamespace;
        private readonly CodeName serviceName;
        private readonly string serializerSubNamespace;
        private readonly EmptyTypeName serializerEmptyType;
        private readonly string? commandTopic;
        private readonly string? telemetryTopic;
        private readonly string? cmdServiceGroupId;
        private readonly string? telemServiceGroupId;
        private readonly List<CommandEnvoyInfo> cmdEnvoyInfos;
        private readonly List<TelemetryEnvoyInfo> telemEnvoyInfos;
        private readonly bool doesCommandTargetExecutor;
        private readonly bool doesCommandTargetService;
        private readonly bool doesTelemetryTargetService;
        private readonly bool generateClient;
        private readonly bool generateServer;
        private readonly bool defaultImpl;

        public DotNetService(
            string projectName,
            CodeName genNamespace,
            CodeName? sharedNamespace,
            CodeName serviceName,
            string serializerSubNamespace,
            EmptyTypeName serializerEmptyType,
            string? commandTopic,
            string? telemetryTopic,
            string? cmdServiceGroupId,
            string? telemServiceGroupId,
            List<CommandEnvoyInfo> cmdEnvoyInfos,
            List<TelemetryEnvoyInfo> telemEnvoyInfos,
            bool doesCommandTargetExecutor,
            bool doesCommandTargetService,
            bool doesTelemetryTargetService,
            bool generateClient,
            bool generateServer,
            bool defaultImpl)
        {
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.sharedNamespace = sharedNamespace;
            this.serviceName = serviceName;
            this.serializerSubNamespace = serializerSubNamespace;
            this.serializerEmptyType = serializerEmptyType;
            this.commandTopic = commandTopic;
            this.telemetryTopic = telemetryTopic;
            this.cmdServiceGroupId = cmdServiceGroupId;
            this.telemServiceGroupId = telemServiceGroupId;
            this.cmdEnvoyInfos = cmdEnvoyInfos;
            this.telemEnvoyInfos = telemEnvoyInfos;
            this.doesCommandTargetExecutor = doesCommandTargetExecutor;
            this.doesCommandTargetService = doesCommandTargetService;
            this.doesTelemetryTargetService = doesTelemetryTargetService;
            this.generateClient = generateClient;
            this.generateServer = generateServer;
            this.defaultImpl = defaultImpl;
        }

        public string FileName { get => $"{this.serviceName.GetFileName(TargetLanguage.CSharp)}.g.cs"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.CSharp); }
    }
}
