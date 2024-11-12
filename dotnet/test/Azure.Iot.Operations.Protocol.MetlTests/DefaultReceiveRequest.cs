namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class DefaultReceiveRequest
    {
        public string? Topic { get; set; }

        public string? Payload { get; set; }

        public string? ContentType { get; set; }

        public int? FormatIndicator { get; set; }

        public int? CorrelationIndex { get; set; }

        public int? Qos { get; set; }

        public TestCaseDuration? MessageExpiry { get; set; }

        public string? ResponseTopic { get; set; }

        public int? InvokerIndex { get; set; }
    }
}
