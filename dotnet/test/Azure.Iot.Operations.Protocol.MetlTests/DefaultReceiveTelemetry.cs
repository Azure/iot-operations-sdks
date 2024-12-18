// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class DefaultReceiveTelemetry
    {
        public string? Topic { get; set; }

        public string? Payload { get; set; }

        public string? ContentType { get; set; }

        public int? FormatIndicator { get; set; }

        public int? Qos { get; set; }

        public TestCaseDuration? MessageExpiry { get; set; }

        public int? SourceIndex { get; set; }
    }
}
