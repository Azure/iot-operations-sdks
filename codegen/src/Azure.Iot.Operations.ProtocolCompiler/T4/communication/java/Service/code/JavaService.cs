
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
            List<(CodeName, ITypeName?, ITypeName?)> cmdNameReqResps,
            List<(CodeName, ITypeName)> telemNameSchemas,
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
            this.cmdNameReqResps = cmdNameReqResps.Select(nrr => (nrr.Item1.AsGiven, nrr.Item2?.GetTypeName(TargetLanguage.Java), nrr.Item3?.GetTypeName(TargetLanguage.Java))).ToList();
            this.telemSchemas = telemNameSchemas.Select(tns => tns.Item2.GetTypeName(TargetLanguage.Java)).ToList();
            this.doesCommandTargetExecutor = doesCommandTargetExecutor;
            this.doesCommandTargetService = doesCommandTargetService;
            this.doesTelemetryTargetService = doesTelemetryTargetService;
        }

        public string FileName { get => $"{this.serviceName}.java"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.Java); }
    }
}
