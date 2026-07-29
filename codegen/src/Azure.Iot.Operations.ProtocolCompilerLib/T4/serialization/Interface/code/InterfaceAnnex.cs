
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

        public static string? PropertyTopic { get; } = AnnexFileProperties.PropertyTopic;

        public static string? CommandRequestTopic { get; } = AnnexFileProperties.CommandRequestTopic;

        public static string? TelemServiceGroupId { get; } = AnnexFileProperties.TelemServiceGroupId;

        public static string? CmdServiceGroupId { get; } = AnnexFileProperties.CmdServiceGroupId;

        public static string? TelemetryList { get; } = AnnexFileProperties.TelemetryList;

        public static string? TelemName { get; } = AnnexFileProperties.TelemName;

        public static string? TelemSchema { get; } = AnnexFileProperties.TelemSchema;

        public static string? PropertyList { get; } = AnnexFileProperties.PropertyList;

        public static string? PropName { get; } = AnnexFileProperties.PropName;

        public static string? PropSchema { get; } = AnnexFileProperties.PropSchema;

        public static string? PropReadRespSchema { get; } = AnnexFileProperties.PropReadRespSchema;

        public static string? PropReadRespNamespace { get; } = AnnexFileProperties.PropReadRespNamespace;

        public static string? PropWriteReqSchema { get; } = AnnexFileProperties.PropWriteReqSchema;

        public static string? PropWriteReqNamespace { get; } = AnnexFileProperties.PropWriteReqNamespace;

        public static string? PropWriteRespSchema { get; } = AnnexFileProperties.PropWriteRespSchema;

        public static string? PropWriteRespNamespace { get; } = AnnexFileProperties.PropWriteRespNamespace;

        public static string? PropValueName { get; } = AnnexFileProperties.PropValueName;

        public static string? PropReadErrorName { get; } = AnnexFileProperties.PropReadErrorName;

        public static string? PropReadErrorSchema { get; } = AnnexFileProperties.PropReadErrorSchema;

        public static string? PropReadErrorNamespace { get; } = AnnexFileProperties.PropReadErrorNamespace;

        public static string? PropWriteErrorName { get; } = AnnexFileProperties.PropWriteErrorName;

        public static string? PropWriteErrorSchema { get; } = AnnexFileProperties.PropWriteErrorSchema;

        public static string? PropWriteErrorNamespace { get; } = AnnexFileProperties.PropWriteErrorNamespace;

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

        public static string? AggregateErrorList { get; } = AnnexFileProperties.AggregateErrorList;

        public static string? InnerErrorList { get; } = AnnexFileProperties.InnerErrorList;

        public static string? InnerErrorName { get; } = AnnexFileProperties.InnerErrorName;

        public static string? InnerErrorSchema { get; } = AnnexFileProperties.InnerErrorSchema;

        public static string? TelemSeparate { get; } = AnnexFileProperties.TelemSeparate;

        public static string? PropSeparate { get; } = AnnexFileProperties.PropSeparate;

        private readonly string projectName;
        private readonly CodeName genNamespace;
        private readonly CodeName? sharedPrefix;
        private readonly string modelId;
        private readonly string serializationFormat;
        private readonly CodeName serviceName;
        private readonly string? telemTopicPattern;
        private readonly string? propTopicPattern;
        private readonly string? cmdTopicPattern;
        private readonly string? telemServiceGroupId;
        private readonly string? cmdServiceGroupId;
        private readonly List<TelemetrySchemaInfo> telemSchemaInfos;
        private readonly List<PropertySchemaInfo> propSchemaInfos;
        private readonly List<CommandSchemaInfo> cmdSchemaInfos;
        private readonly List<ErrorSchemaInfo> errSchemaInfos;
        private readonly List<AggregateErrorSchemaInfo> aggregateErrSchemaInfos;
        private readonly bool separateTelemetries;
        private readonly bool separateProperties;

        public InterfaceAnnex(
            string projectName,
            CodeName genNamespace,
            CodeName? sharedPrefix,
            string modelId,
            string serializationFormat,
            CodeName serviceName,
            string? telemTopicPattern,
            string? propTopicPattern,
            string? cmdTopicPattern,
            string? telemServiceGroupId,
            string? cmdServiceGroupId,
            List<TelemetrySchemaInfo> telemSchemaInfos,
            List<PropertySchemaInfo> propSchemaInfos,
            List<CommandSchemaInfo> cmdSchemaInfos,
            List<ErrorSchemaInfo> errSchemaInfos,
            List<AggregateErrorSchemaInfo> aggregateErrSchemaInfos,
            bool separateTelemetries,
            bool separateProperties)
        {
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.sharedPrefix = sharedPrefix;
            this.modelId = modelId;
            this.serializationFormat = serializationFormat;
            this.serviceName = serviceName;
            this.telemTopicPattern = telemTopicPattern;
            this.propTopicPattern = propTopicPattern;
            this.cmdTopicPattern = cmdTopicPattern;
            this.telemServiceGroupId = telemServiceGroupId;
            this.cmdServiceGroupId = cmdServiceGroupId;
            this.telemSchemaInfos = telemSchemaInfos;
            this.propSchemaInfos = propSchemaInfos;
            this.cmdSchemaInfos = cmdSchemaInfos;
            this.errSchemaInfos = errSchemaInfos;
            this.aggregateErrSchemaInfos = aggregateErrSchemaInfos;
            this.separateTelemetries = separateTelemetries;
            this.separateProperties = separateProperties;
        }

        public string FileName { get => $"{this.serviceName.GetFileName(TargetLanguage.Independent)}.annex.json"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.Independent); }
    }
}
