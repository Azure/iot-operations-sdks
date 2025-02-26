// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class TestCaseReceiver
    {
        public static TestCaseSerializer DefaultSerializer = new();
        public static string? DefaultTelemetryTopic;
        public static string? DefaultTopicNamespace;

        public TestCaseSerializer Serializer { get; set; } = DefaultSerializer;

        public string? TelemetryTopic { get; set; } = DefaultTelemetryTopic;

        public string? TopicNamespace { get; set; } = DefaultTopicNamespace;

        public Dictionary<string, string>? TopicTokenMap { get; set; }

        public TestCaseError? RaiseError { get; set; }
    }
}
