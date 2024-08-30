namespace Azure.Iot.Operations.Protocol.UnitTests.Protocol
{
    public class TestCaseExecutor
    {
        public static string? DefaultCommandName;
        public static string? DefaultRequestTopic;
        public static string? DefaultModelId;
        public static string? DefaultExecutorId;
        public static string? DefaultTopicNamespace;
        public static bool DefaultIdempotent;
        public static TestCaseDuration? DefaultCacheableDuration;
        public static TestCaseDuration? DefaultExecutorTimeout;
        public static Dictionary<string, string[]>? DefaultRequestResponsesMap;
        public static int? DefaultExecutionConcurrency;

        public string? CommandName { get; set; } = DefaultCommandName;

        public string? RequestTopic { get; set; } = DefaultRequestTopic;

        public string? ModelId { get; set; } = DefaultModelId;

        public string? ExecutorId { get; set; } = DefaultExecutorId;

        public string? TopicNamespace { get; set; } = DefaultTopicNamespace;

        public bool Idempotent { get; set; } = DefaultIdempotent;

        public TestCaseDuration? CacheableDuration { get; set; } = DefaultCacheableDuration;

        public TestCaseDuration? ExecutionTimeout { get; set; } = DefaultExecutorTimeout;

        public Dictionary<string, string[]> RequestResponsesMap { get; set; } = DefaultRequestResponsesMap ?? new();

        public Dictionary<string, string?> ResponseMetadata { get; set; } = new();

        public int? ExecutionConcurrency { get; set; } = DefaultExecutionConcurrency;

        public TestCaseError? RaiseError { get; set; }

        public List<TestCaseSync> Sync { get; set; } = new();
    }
}
