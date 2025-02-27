// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class TestCaseInvoker
    {
        public static string? DefaultCommandName;
        public static TestCaseSerializer DefaultSerializer = new();
        public static string? DefaultRequestTopic;
        public static string? DefaultTopicNamespace;
        public static string? DefaultResponseTopicPrefix;
        public static string? DefaultResponseTopicSuffix;

        public string? CommandName { get; set; } = DefaultCommandName;

        public TestCaseSerializer Serializer { get; set; } = DefaultSerializer;

        public string? RequestTopic { get; set; } = DefaultRequestTopic;

        public string? TopicNamespace { get; set; } = DefaultTopicNamespace;

        public string? ResponseTopicPrefix { get; set; } = DefaultResponseTopicPrefix;

        public string? ResponseTopicSuffix { get; set; } = DefaultResponseTopicSuffix;

        public Dictionary<string, string>? TopicTokenMap { get; set; }

        public string? ResponseTopicPattern { get; set; }
    }
}
