// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class TestCaseSender
    {
        public static string? DefaultTelemetryName;
        public static string? DefaultTelemetryTopic;
        public static string? DefaultDataSchema;
        public static string? DefaultTopicNamespace;

        public string? TelemetryName { get; set; } = DefaultTelemetryName;

        public string? TelemetryTopic { get; set; } = DefaultTelemetryTopic;

        public string? DataSchema { get; set; } = DefaultDataSchema;

        public string? TopicNamespace { get; set; } = DefaultTopicNamespace;

        public Dictionary<string, string>? TopicTokenMap { get; set; }
    }
}
