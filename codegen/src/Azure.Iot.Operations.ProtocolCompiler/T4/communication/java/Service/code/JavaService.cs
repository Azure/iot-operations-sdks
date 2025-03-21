
namespace Azure.Iot.Operations.ProtocolCompiler
{
    public partial class JavaService : ITemplateTransform
    {
        private readonly CodeName genNamespace;
        private readonly string modelId;
        private readonly CodeName serviceName;
        private readonly string? commandTopic;
        private readonly string? telemetryTopic;
        private readonly string? cmdServiceGroupId;
        private readonly string? telemServiceGroupId;
        private readonly List<(string, string?, string?)> cmdNameReqResps;
        private readonly List<string> telemSchemas;
        private readonly bool doesCommandTargetExecutor;
        private readonly bool doesCommandTargetService;
        private readonly bool doesTelemetryTargetService;

        public JavaService(
            CodeName genNamespace,
            string modelId,
            CodeName serviceName,
            string? commandTopic,
            string? telemetryTopic,
            string? cmdServiceGroupId,
            string? telemServiceGroupId,
            List<CommandEnvoyInfo> cmdEnvoyInfos,
            List<TelemetryEnvoyInfo> telemEnvoyInfos,
            bool doesCommandTargetExecutor,
            bool doesCommandTargetService,
            bool doesTelemetryTargetService)
        {
            this.genNamespace = genNamespace;
            this.modelId = modelId;
            this.serviceName = serviceName;
            this.commandTopic = commandTopic;
            this.telemetryTopic = telemetryTopic;
            this.cmdServiceGroupId = cmdServiceGroupId;
            this.telemServiceGroupId = telemServiceGroupId;
            this.cmdNameReqResps = cmdEnvoyInfos.Select(cei => (cei.Name.AsGiven, cei.RequestSchema?.GetTypeName(TargetLanguage.Java), cei.ResponseSchema?.GetTypeName(TargetLanguage.Java))).ToList();
            this.telemSchemas = telemEnvoyInfos.Select(tei => tei.Schema.GetTypeName(TargetLanguage.Java)).ToList();
            this.doesCommandTargetExecutor = doesCommandTargetExecutor;
            this.doesCommandTargetService = doesCommandTargetService;
            this.doesTelemetryTargetService = doesTelemetryTargetService;
        }

        public string FileName { get => $"{this.serviceName}.java"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.Java); }
    }
}
