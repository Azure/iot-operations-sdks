namespace Azure.Iot.Operations.Protocol.UnitTests.Protocol
{
    public class DefaultExecutor
    {
        public string? CommandName { get; set; }

        public string? RequestTopic { get; set; }

        public string? ModelId { get; set; }

        public string? ExecutorId { get; set; }

        public string? TopicNamespace { get; set; }

        public bool Idempotent { get; set; }

        public TestCaseDuration? CacheTtl { get; set; }

        public TestCaseDuration? ExecutionTimeout { get; set; }

        public Dictionary<string, string[]>? RequestResponsesMap { get; set; }

        public int? ExecutionConcurrency { get; set; }
    }
}
