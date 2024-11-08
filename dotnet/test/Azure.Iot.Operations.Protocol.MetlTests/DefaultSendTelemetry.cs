namespace Azure.Iot.Operations.Protocol.UnitTests.Protocol
{
    public class DefaultSendTelemetry
    {
        public string? TelemetryName { get; set; }

        public string? TelemetryValue { get; set; }

        public TestCaseDuration? Timeout { get; set; }

        public int? Qos { get; set; }
    }
}
