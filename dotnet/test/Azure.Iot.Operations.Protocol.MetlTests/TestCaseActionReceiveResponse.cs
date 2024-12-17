namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class TestCaseActionReceiveResponse : TestCaseAction
    {
        public static string? DefaultTopic;
        public static string? DefaultPayload;
        public static string? DefaultContentType;
        public static int? DefaultFormatIndicator;
        public static int? DefaultCorrelationIndex;
        public static int? DefaultQos;
        public static TestCaseDuration? DefaultMessageExpiry;
        public static string? DefaultStatus;
        public static string? DefaultStatusMessage;
        public static string? DefaultIsApplicationError;
        public static string? DefaultInvalidPropertyName;
        public static string? DefaultInvalidPropertyValue;

        public string? Topic { get; set; } = DefaultTopic;

        public string? Payload { get; set; } = DefaultPayload;

        public bool BypassSerialization { get; set; }

        public string? ContentType { get; set; } = DefaultContentType;

        public int? FormatIndicator { get; set; } = DefaultFormatIndicator;

        public Dictionary<string, string> Metadata { get; set; } = new();

        public int? CorrelationIndex { get; set; } = DefaultCorrelationIndex;

        public int? Qos { get; set; } = DefaultQos;

        public TestCaseDuration? MessageExpiry { get; set; } = DefaultMessageExpiry;

        public string? Status { get; set; } = DefaultStatus;

        public string? StatusMessage { get; set; } = DefaultStatusMessage;

        public string? IsApplicationError { get; set; } = DefaultIsApplicationError;

        public string? InvalidPropertyName { get; set; } = DefaultInvalidPropertyName;

        public string? InvalidPropertyValue { get; set; } = DefaultInvalidPropertyValue;

        public int? PacketIndex { get; set; }
    }
}
