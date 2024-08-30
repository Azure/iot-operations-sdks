namespace Azure.Iot.Operations.Protocol.UnitTests.Protocol
{
    public class TestCaseInvoker
    {
        public static string? DefaultCommandName;
        public static string? DefaultRequestTopic;
        public static string? DefaultModelId;
        public static string? DefaultResponseTopicPrefix;
        public static string? DefaultResponseTopicSuffix;

        public string? CommandName { get; set; } = DefaultCommandName;

        public string? RequestTopic { get; set; } = DefaultRequestTopic;

        public string? ModelId { get; set; } = DefaultModelId;

        public string? TopicNamespace { get; set; }

        public string? ResponseTopicPrefix { get; set; } = DefaultResponseTopicPrefix;

        public string? ResponseTopicSuffix { get; set; } = DefaultResponseTopicSuffix;

        public Dictionary<string, string>? CustomTokenMap { get; set; }

        public Dictionary<string, string?>? ResponseTopicMap { get; set; }
    }
}
