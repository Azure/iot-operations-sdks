// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class TestCaseReceivedTelemetry
    {
        private static readonly TestCaseCloudEvent IrrelevantCloudEvent = new() { Irrelevant = true };

        public object TelemetryValue { get; set; } = false;

        public Dictionary<string, string?> Metadata { get; set; } = new();

        public TestCaseCloudEvent? CloudEvent { get; set; } = IrrelevantCloudEvent;

        public int? SourceIndex { get; set; }
    }
}
