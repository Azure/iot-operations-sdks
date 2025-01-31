
namespace Azure.Iot.Operations.ProtocolCompiler
{
    public partial class DotNetService : ITemplateTransform
    {
        private readonly string projectName;
        private readonly CodeName genNamespace;
        private readonly CodeName serviceName;
        private readonly string serializerSubNamespace;
        private readonly EmptyTypeName serializerEmptyType;
        private readonly string? commandTopic;
        private readonly string? telemetryTopic;
        private readonly string? cmdServiceGroupId;
        private readonly string? telemServiceGroupId;
        private readonly List<(CodeName, ITypeName?, ITypeName?)> cmdNameReqResps;
        private readonly List<(CodeName, ITypeName)> telemNameSchemas;
        private readonly bool doesCommandTargetExecutor;
        private readonly bool doesCommandTargetService;
        private readonly bool doesTelemetryTargetService;
        private readonly bool syncApi;
        private readonly bool generateClient;
        private readonly bool generateServer;

        public DotNetService(
            string projectName,
            CodeName genNamespace,
            CodeName serviceName,
            string serializerSubNamespace,
            EmptyTypeName serializerEmptyType,
            string? commandTopic,
            string? telemetryTopic,
            string? cmdServiceGroupId,
            string? telemServiceGroupId,
            List<(CodeName, ITypeName?, ITypeName?)> cmdNameReqResps,
            List<(CodeName, ITypeName)> telemNameSchemas,
            bool doesCommandTargetExecutor,
            bool doesCommandTargetService,
            bool doesTelemetryTargetService,
            bool syncApi,
            bool generateClient,
            bool generateServer)
        {
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.serviceName = serviceName;
            this.serializerSubNamespace = serializerSubNamespace;
            this.serializerEmptyType = serializerEmptyType;
            this.commandTopic = commandTopic;
            this.telemetryTopic = telemetryTopic;
            this.cmdServiceGroupId = cmdServiceGroupId;
            this.telemServiceGroupId = telemServiceGroupId;
            this.cmdNameReqResps = cmdNameReqResps;
            this.telemNameSchemas = telemNameSchemas;
            this.doesCommandTargetExecutor = doesCommandTargetExecutor;
            this.doesCommandTargetService = doesCommandTargetService;
            this.doesTelemetryTargetService = doesTelemetryTargetService;
            this.syncApi = syncApi;
            this.generateClient = generateClient;
            this.generateServer = generateServer;
        }

        public string FileName { get => $"{this.serviceName.GetFileName(TargetLanguage.CSharp)}.g.cs"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.CSharp); }
    }
}
