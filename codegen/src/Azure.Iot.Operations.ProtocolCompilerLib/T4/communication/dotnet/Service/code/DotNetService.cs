
namespace Azure.Iot.Operations.ProtocolCompilerLib
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
        private readonly string? propertyTopic;
        private readonly string? telemetryTopic;
        private readonly string? cmdServiceGroupId;
        private readonly string? telemServiceGroupId;
        private readonly List<CommandEnvoyInfo> cmdEnvoyInfos;
        private readonly List<PropertyEnvoyInfo> propEnvoyInfos;
        private readonly List<TelemetryEnvoyInfo> telemEnvoyInfos;
        private readonly bool doesCommandTargetExecutor;
        private readonly bool doesCommandTargetService;
        private readonly bool doesPropertyTargetMaintainer;
        private readonly bool doesTelemetryTargetService;
        private readonly bool generateClient;
        private readonly bool generateServer;
        private readonly bool defaultImpl;
        private readonly bool separateProperties;

        public DotNetService(
            string projectName,
            CodeName genNamespace,
            CodeName? sharedNamespace,
            CodeName serviceName,
            string serializerSubNamespace,
            EmptyTypeName serializerEmptyType,
            string? commandTopic,
            string? propertyTopic,
            string? telemetryTopic,
            string? cmdServiceGroupId,
            string? telemServiceGroupId,
            List<CommandEnvoyInfo> cmdEnvoyInfos,
            List<PropertyEnvoyInfo> propEnvoyInfos,
            List<TelemetryEnvoyInfo> telemEnvoyInfos,
            bool doesCommandTargetExecutor,
            bool doesCommandTargetService,
            bool doesPropertyTargetMaintainer,
            bool doesTelemetryTargetService,
            bool generateClient,
            bool generateServer,
            bool defaultImpl,
            bool separateProperties)
        {
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.sharedNamespace = sharedNamespace;
            this.serviceName = serviceName;
            this.serializerSubNamespace = serializerSubNamespace;
            this.serializerEmptyType = serializerEmptyType;
            this.commandTopic = commandTopic;
            this.propertyTopic = propertyTopic;
            this.telemetryTopic = telemetryTopic;
            this.cmdServiceGroupId = cmdServiceGroupId;
            this.telemServiceGroupId = telemServiceGroupId;
            this.cmdEnvoyInfos = cmdEnvoyInfos;
            this.propEnvoyInfos = propEnvoyInfos;
            this.telemEnvoyInfos = telemEnvoyInfos;
            this.doesCommandTargetExecutor = doesCommandTargetExecutor;
            this.doesCommandTargetService = doesCommandTargetService;
            this.doesPropertyTargetMaintainer = doesPropertyTargetMaintainer;
            this.doesTelemetryTargetService = doesTelemetryTargetService;
            this.generateClient = generateClient;
            this.generateServer = generateServer;
            this.defaultImpl = defaultImpl;
            this.separateProperties = separateProperties;
        }

        public string FileName { get => $"{this.serviceName.GetFileName(TargetLanguage.CSharp)}.g.cs"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.CSharp); }
    }
}
