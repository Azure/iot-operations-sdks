// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class TestCaseEpilogue
    {
        public List<string> SubscribedTopics { get; set; } = new();

        public int? PublicationCount { get; set; }

        public List<TestCasePublishedMessage> PublishedMessages { get; set; } = new();

        public int? AcknowledgementCount {  get; set; }

        public List<TestCaseReceivedTelemetry> ReceivedTelemetries { get; set; } = new();

        public int? ExecutionCount { get; set; }

        public Dictionary<int, int> ExecutionCounts { get; set; } = new();

        public int? TelemetryCount { get; set; }

        public Dictionary<int, int> TelemetryCounts { get; set; } = new();

        public TestCaseCatch? Catch { get; set; }
    }
}
