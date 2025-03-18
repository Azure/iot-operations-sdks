// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class TestCaseActionSendTelemetry : TestCaseAction
    {
        public static string? DefaultTelemetryName;
        public static string? DefaultTelemetryValue;
        public static TestCaseDuration? DefaultTimeout;
        public static int? DefaultQos;

        public string? TelemetryName { get; set; } = DefaultTelemetryName;

        public Dictionary<string, string>? TopicTokenMap { get; set; }

        public TestCaseDuration? Timeout { get; set; } = DefaultTimeout;

        public string? TelemetryValue { get; set; } = DefaultTelemetryValue;

        public Dictionary<string, string>? Metadata { get; set; }

        public TestCaseCloudEvent? CloudEvent { get; set; }

        public int? Qos { get; set; } = DefaultQos;
    }
}
