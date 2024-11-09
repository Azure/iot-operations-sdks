namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class TestCaseReceivedTelemetry
    {
        public string? TelemetryValue { get; set; }

        public Dictionary<string, string?> Metadata { get; set; } = new();

        public TestCaseCloudEvent? CloudEvent { get; set; }

        public int? SenderIndex { get; set; }
    }
}
