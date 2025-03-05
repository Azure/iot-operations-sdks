// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class TestCaseExecutor
    {
        public static string? DefaultCommandName;
        public static TestCaseSerializer DefaultSerializer = new();
        public static string? DefaultRequestTopic;
        public static string? DefaultExecutorId;
        public static string? DefaultTopicNamespace;
        public static bool DefaultIdempotent;
        public static TestCaseDuration? DefaultCacheTtl;
        public static TestCaseDuration? DefaultExecutorTimeout;
        public static Dictionary<string, string[]>? DefaultRequestResponsesMap;
        public static int? DefaultExecutionConcurrency;

        public string? CommandName { get; set; } = DefaultCommandName;

        public TestCaseSerializer Serializer { get; set; } = DefaultSerializer;

        public string? RequestTopic { get; set; } = DefaultRequestTopic;

        public string? ExecutorId { get; set; } = DefaultExecutorId;

        public string? TopicNamespace { get; set; } = DefaultTopicNamespace;

        public Dictionary<string, string>? TopicTokenMap { get; set; }

        public bool Idempotent { get; set; } = DefaultIdempotent;

        public TestCaseDuration? CacheTtl { get; set; } = DefaultCacheTtl;

        public TestCaseDuration? ExecutionTimeout { get; set; } = DefaultExecutorTimeout;

        public Dictionary<string, string[]> RequestResponsesMap { get; set; } = DefaultRequestResponsesMap ?? new();

        public Dictionary<string, string?> ResponseMetadata { get; set; } = new();

        public string? TokenMetadataPrefix { get; set; }

        public int? ExecutionConcurrency { get; set; } = DefaultExecutionConcurrency;

        public bool RaiseError { get; set; }

        public List<TestCaseSync> Sync { get; set; } = new();
    }
}
