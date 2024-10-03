
namespace Azure.Iot.Operations.ProtocolCompiler
{
    public partial class DotNetService : ITemplateTransform
    {
        private readonly string projectName;
        private readonly string genNamespace;
        private readonly string modelId;
        private readonly string serviceName;
        private readonly string serializerSubNamespace;
        private readonly string serializerEmptyType;
        private readonly string allocateEmpty;
        private readonly string? commandTopic;
        private readonly string? telemetryTopic;
        private readonly List<(string, string?, string?)> cmdNameReqResps;
        private readonly List<string> telemSchemas;
        private readonly bool doesCommandTargetExecutor;
        private readonly bool doesCommandTargetService;
        private readonly bool doesTelemetryTargetService;
        private readonly bool syncApi;

        public DotNetService(
            string projectName,
            string genNamespace,
            string modelId,
            string serviceName,
            string serializerSubNamespace,
            string serializerEmptyType,
            string? commandTopic,
            string? telemetryTopic,
            List<(string, string?, string?)> cmdNameReqResps,
            List<string> telemSchemas,
            bool doesCommandTargetExecutor,
            bool doesCommandTargetService,
            bool doesTelemetryTargetService,
            bool syncApi)
        {
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.modelId = modelId;
            this.serviceName = serviceName;
            this.serializerSubNamespace = serializerSubNamespace;
            this.serializerEmptyType = serializerEmptyType == "" ? "byte[]" : serializerEmptyType;
            this.allocateEmpty = serializerEmptyType == "" ? "Array.Empty<byte>()" : $"new {serializerEmptyType}()";
            this.commandTopic = commandTopic;
            this.telemetryTopic = telemetryTopic;
            this.cmdNameReqResps = cmdNameReqResps;
            this.telemSchemas = telemSchemas;
            this.doesCommandTargetExecutor = doesCommandTargetExecutor;
            this.doesCommandTargetService = doesCommandTargetService;
            this.doesTelemetryTargetService = doesTelemetryTargetService;
            this.syncApi = syncApi;
        }

        public string FileName { get => $"{this.serviceName}.g.cs"; }

        public string FolderPath { get => this.genNamespace; }
    }
}
