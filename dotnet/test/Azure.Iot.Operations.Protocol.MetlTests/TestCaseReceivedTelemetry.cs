// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class TestCaseReceivedTelemetry
    {
        public object TelemetryValue { get; set; } = false;

        public Dictionary<string, string?> Metadata { get; set; } = new();

        public TestCaseCloudEvent? CloudEvent { get; set; }

        public int? SourceIndex { get; set; }
    }
}
