
namespace Azure.Iot.Operations.ProtocolCompiler
{
    public partial class PythonService : ITemplateTransform
    {
        private readonly CodeName genNamespace;
        private readonly string modelId;
        private readonly CodeName serviceName;
        private readonly string? commandTopic;
        private readonly string? telemetryTopic;
        private readonly string? cmdServiceGroupId;
        private readonly string? telemServiceGroupId;
        private readonly List<(string, string, string)> cmdNameReqResps;
        private readonly List<string> telemSchemas;
        private readonly bool doesCommandTargetExecutor;
        private readonly bool doesCommandTargetService;
        private readonly bool doesTelemetryTargetService;

        public PythonService(
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
            this.cmdNameReqResps = cmdNameReqResps.Select(nrr => (nrr.Item1.AsGiven, ToPythonSchema(nrr.Item2?.GetTypeName(TargetLanguage.Python)), ToPythonSchema(nrr.Item3?.GetTypeName(TargetLanguage.Python)))).ToList();
            this.telemSchemas = telemNameSchemas.Select(tns => tns.Item2.GetTypeName(TargetLanguage.Python)).ToList();
            this.doesCommandTargetExecutor = doesCommandTargetExecutor;
            this.doesCommandTargetService = doesCommandTargetService;
            this.doesTelemetryTargetService = doesTelemetryTargetService;
        }

        public string FileName { get => $"{this.serviceName.GetFileName(TargetLanguage.Python)}_g.py"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.Python); }

        private static string ToPythonSchema(string? schema)
        {
            return schema == "" ? "any" : schema ?? "None";
        }
    }
}
