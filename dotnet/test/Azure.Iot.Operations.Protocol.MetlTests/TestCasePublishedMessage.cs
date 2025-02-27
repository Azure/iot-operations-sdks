// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class TestCasePublishedMessage
    {
        public int? CorrelationIndex { get; set; }

        public string? Topic { get; set; }

        public object Payload { get; set; } = false;

        public string? ContentType { get; set; }

        public int? FormatIndicator { get; set; }

        public Dictionary<string, string?> Metadata { get; set; } = new();

        public object CommandStatus { get; set; } = false;

        public bool? IsApplicationError { get; set; }

        public string? SourceId { get; set; }

        public int? Expiry { get; set; }
    }
}
