
namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    public partial class InterfaceAnnex : ITemplateTransform
    {
        public static string? ProjectName { get; } = AnnexFileProperties.ProjectName;

        public static string? Namespace { get; } = AnnexFileProperties.Namespace;

        public static string? Shared { get; } = AnnexFileProperties.Shared;

        public static string? ModelId { get; } = AnnexFileProperties.ModelId;

        public static string? ServiceName { get; } = AnnexFileProperties.ServiceName;

        public static string? PayloadFormat { get; } = AnnexFileProperties.PayloadFormat;

        public static string? TelemetryTopic { get; } = AnnexFileProperties.TelemetryTopic;

        public static string? CommandRequestTopic { get; } = AnnexFileProperties.CommandRequestTopic;

        public static string? TelemServiceGroupId { get; } = AnnexFileProperties.TelemServiceGroupId;

        public static string? CmdServiceGroupId { get; } = AnnexFileProperties.CmdServiceGroupId;

        public static string? TelemetryList { get; } = AnnexFileProperties.TelemetryList;

        public static string? TelemName { get; } = AnnexFileProperties.TelemName;

        public static string? TelemSchema { get; } = AnnexFileProperties.TelemSchema;

        public static string? CommandList { get; } = AnnexFileProperties.CommandList;

        public static string? CommandName { get; } = AnnexFileProperties.CommandName;

        public static string? CmdRequestSchema { get; } = AnnexFileProperties.CmdRequestSchema;

        public static string? CmdResponseSchema { get; } = AnnexFileProperties.CmdResponseSchema;

        public static string? CmdRequestNamespace { get; } = AnnexFileProperties.CmdRequestNamespace;

        public static string? CmdResponseNamespace { get; } = AnnexFileProperties.CmdResponseNamespace;
    
        public static string? NormalResultName { get; } = AnnexFileProperties.NormalResultName;

        public static string? NormalResultSchema { get; } = AnnexFileProperties.NormalResultSchema;

        public static string? NormalResultNamespace { get; } = AnnexFileProperties.NormalResultNamespace;

        public static string? ErrorResultName { get; } = AnnexFileProperties.ErrorResultName;

        public static string? ErrorResultSchema { get; } = AnnexFileProperties.ErrorResultSchema;

        public static string? ErrorResultNamespace { get; } = AnnexFileProperties.ErrorResultNamespace;

        public static string? ErrorCodeName { get; } = AnnexFileProperties.ErrorCodeName;

        public static string? ErrorCodeSchema { get; } = AnnexFileProperties.ErrorCodeSchema;

        public static string? ErrorCodeNamespace { get; } = AnnexFileProperties.ErrorCodeNamespace;

        public static string? ErrorCodeEnumeration { get; } = AnnexFileProperties.ErrorCodeEnumeration;

        public static string? ErrorInfoName { get; } = AnnexFileProperties.ErrorInfoName;

        public static string? ErrorInfoSchema { get; } = AnnexFileProperties.ErrorInfoSchema;

        public static string? ErrorInfoNamespace { get; } = AnnexFileProperties.ErrorInfoNamespace;

        public static string? RequestIsNullable { get; } = AnnexFileProperties.RequestIsNullable;

        public static string? ResponseIsNullable { get; } = AnnexFileProperties.ResponseIsNullable;

        public static string? CmdIsIdempotent { get; } = AnnexFileProperties.CmdIsIdempotent;

        public static string? CmdCacheability { get; } = AnnexFileProperties.Cacheability;

        public static string? ErrorList { get; } = AnnexFileProperties.ErrorList;

        public static string? ErrorSchema { get; } = AnnexFileProperties.ErrorSchema;

        public static string? ErrorNamespace { get; } = AnnexFileProperties.ErrorNamespace;

        public static string? ErrorDescription { get; } = AnnexFileProperties.ErrorDescription;

        public static string? ErrorMessageField { get; } = AnnexFileProperties.ErrorMessageField;

        public static string? ErrorMessageIsNullable { get; } = AnnexFileProperties.ErrorMessageIsNullable;

        public static string? TelemSeparate { get; } = AnnexFileProperties.TelemSeparate;

        private readonly string projectName;
        private readonly CodeName genNamespace;
        private readonly CodeName? sharedPrefix;
        private readonly string modelId;
        private readonly string serializationFormat;
        private readonly CodeName serviceName;
        private readonly string? telemTopicPattern;
        private readonly string? cmdTopicPattern;
        private readonly string? telemServiceGroupId;
        private readonly string? cmdServiceGroupId;
        private readonly List<TelemetrySchemaInfo> telemSchemaInfos;
        private readonly List<CommandSchemaInfo> cmdSchemaInfos;
        private readonly List<ErrorSchemaInfo> errSchemaInfos;
        private readonly bool separateTelemetries;

        public InterfaceAnnex(
            string projectName,
            CodeName genNamespace,
            CodeName? sharedPrefix,
            string modelId,
            string serializationFormat,
            CodeName serviceName,
            string? telemTopicPattern,
            string? cmdTopicPattern,
            string? telemServiceGroupId,
            string? cmdServiceGroupId,
            List<TelemetrySchemaInfo> telemSchemaInfos,
            List<CommandSchemaInfo> cmdSchemaInfos,
            List<ErrorSchemaInfo> errSchemaInfos,
            bool separateTelemetries)
        {
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.sharedPrefix = sharedPrefix;
            this.modelId = modelId;
            this.serializationFormat = serializationFormat;
            this.serviceName = serviceName;
            this.telemTopicPattern = telemTopicPattern;
            this.cmdTopicPattern = cmdTopicPattern;
            this.telemServiceGroupId = telemServiceGroupId;
            this.cmdServiceGroupId = cmdServiceGroupId;
            this.telemSchemaInfos = telemSchemaInfos;
            this.cmdSchemaInfos = cmdSchemaInfos;
            this.errSchemaInfos = errSchemaInfos;
            this.separateTelemetries = separateTelemetries;
        }

        public string FileName { get => $"{this.serviceName.GetFileName(TargetLanguage.Independent)}.annex.json"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.Independent); }
    }
}
