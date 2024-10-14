
namespace Azure.Iot.Operations.ProtocolCompiler
{
    public partial class InterfaceAnnex : ITemplateTransform
    {
        public static string? ProjectName { get; } = AnnexFileProperties.ProjectName;

        public static string? Namespace { get; } = AnnexFileProperties.Namespace;

        public static string? ModelId { get; } = AnnexFileProperties.ModelId;

        public static string? ServiceName { get; } = AnnexFileProperties.ServiceName;

        public static string? PayloadFormat { get; } = AnnexFileProperties.PayloadFormat;

        public static string? TelemetryTopic { get; } = AnnexFileProperties.TelemetryTopic;

        public static string? CommandRequestTopic { get; } = AnnexFileProperties.CommandRequestTopic;

        public static string? ServiceGroupId { get; } = AnnexFileProperties.ServiceGroupId;

        public static string? TelemetryList { get; } = AnnexFileProperties.TelemetryList;

        public static string? TelemName { get; } = AnnexFileProperties.TelemName;

        public static string? TelemSchema { get; } = AnnexFileProperties.TelemSchema;

        public static string? CommandList { get; } = AnnexFileProperties.CommandList;

        public static string? CommandName { get; } = AnnexFileProperties.CommandName;

        public static string? CmdRequestSchema { get; } = AnnexFileProperties.CmdRequestSchema;

        public static string? CmdResponseSchema { get; } = AnnexFileProperties.CmdResponseSchema;

        public static string? CmdIsIdempotent { get; } = AnnexFileProperties.CmdIsIdempotent;

        public static string? CmdCacheability { get; } = AnnexFileProperties.Cacheability;

        private readonly string projectName;
        private readonly string genNamespace;
        private readonly string modelId;
        private readonly string serializationFormat;
        private readonly string serviceName;
        private readonly string? telemTopicPattern;
        private readonly string? cmdTopicPattern;
        private readonly string? serviceGroupId;
        private readonly List<(string?, string)> telemNameSchemas;
        private readonly List<(string, string?, string?, bool, string?)> cmdNameReqRespIdemStales;

        public InterfaceAnnex(string projectName, string genNamespace, string modelId, string serializationFormat, string serviceName, string? telemTopicPattern, string? cmdTopicPattern, string? serviceGroupId, List<(string?, string)> telemNameSchemas, List<(string, string?, string?, bool, string?)> cmdNameReqRespIdemStales)
        {
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.modelId = modelId;
            this.serializationFormat = serializationFormat;
            this.serviceName = serviceName;
            this.telemTopicPattern = telemTopicPattern;
            this.cmdTopicPattern = cmdTopicPattern;
            this.serviceGroupId = serviceGroupId;
            this.telemNameSchemas = telemNameSchemas;
            this.cmdNameReqRespIdemStales = cmdNameReqRespIdemStales;
        }

        public string FileName { get => $"{this.serviceName}.annex.json"; }

        public string FolderPath { get => this.genNamespace; }
    }
}
