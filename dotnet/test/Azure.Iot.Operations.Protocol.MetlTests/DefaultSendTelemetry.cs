// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class DefaultSendTelemetry
    {
        public string? TelemetryName { get; set; }

        public string? TelemetryValue { get; set; }

        public TestCaseDuration? Timeout { get; set; }

        public int? Qos { get; set; }
    }
}
