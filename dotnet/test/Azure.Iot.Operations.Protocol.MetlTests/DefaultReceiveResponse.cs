namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class DefaultReceiveResponse
    {
        public string? Topic { get; set; }

        public string? Payload { get; set; }

        public string? ContentType { get; set; }

        public int? FormatIndicator { get; set; }

        public int? CorrelationIndex { get; set; }

        public int? Qos { get; set; }

        public TestCaseDuration? MessageExpiry { get; set; }

        public string? Status { get; set; }

        public string? StatusMessage { get; set; }

        public string? IsApplicationError { get; set; }

        public string? InvalidPropertyName { get; set; }

        public string? InvalidPropertyValue { get; set; }
    }
}
