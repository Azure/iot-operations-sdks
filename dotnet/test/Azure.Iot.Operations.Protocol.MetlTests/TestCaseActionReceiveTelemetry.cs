// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class TestCaseActionReceiveTelemetry : TestCaseAction
    {
        public static string? DefaultTopic;
        public static string? DefaultPayload;
        public static string? DefaultContentType;
        public static int? DefaultFormatIndicator;
        public static int? DefaultQos;
        public static TestCaseDuration? DefaultMessageExpiry;
        public static int? DefaultSourceIndex;

        public string? Topic { get; set; } = DefaultTopic;

        public string? Payload { get; set; } = DefaultPayload;

        public bool BypassSerialization { get; set; }

        public string? ContentType { get; set; } = DefaultContentType;

        public int? FormatIndicator { get; set; } = DefaultFormatIndicator;

        public Dictionary<string, string> Metadata { get; set; } = new();

        public int? Qos { get; set; } = DefaultQos;

        public TestCaseDuration? MessageExpiry { get; set; } = DefaultMessageExpiry;

        public int? SourceIndex { get; set; } = DefaultSourceIndex;

        public int? PacketIndex { get; set; }
    }
}
