
namespace Azure.Iot.Operations.ProtocolCompiler
{
    public partial class PythonService : ITemplateTransform
    {
        private readonly string genNamespace;
        private readonly string modelId;
        private readonly string serviceName;
        private readonly string? commandTopic;
        private readonly string? telemetryTopic;
        private readonly List<(string, string, string)> cmdNameReqResps;
        private readonly List<string> telemSchemas;
        private readonly bool doesCommandTargetExecutor;
        private readonly bool doesCommandTargetService;
        private readonly bool doesTelemetryTargetService;

        public PythonService(
            string genNamespace,
            string modelId,
            string serviceName,
            string? commandTopic,
            string? telemetryTopic,
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
            this.cmdNameReqResps = cmdNameReqResps.Select(nrr => (nrr.Item1, ToPythonSchema(nrr.Item2), ToPythonSchema(nrr.Item3))).ToList();
            this.telemSchemas = telemSchemas;
            this.doesCommandTargetExecutor = doesCommandTargetExecutor;
            this.doesCommandTargetService = doesCommandTargetService;
            this.doesTelemetryTargetService = doesTelemetryTargetService;
        }

        public string FileName { get => $"{this.serviceName}_g.py"; }

        public string FolderPath { get => this.genNamespace; }

        private static string ToPythonSchema(string? schema)
        {
            return schema == "" ? "any" : schema ?? "None";
        }
    }
}
