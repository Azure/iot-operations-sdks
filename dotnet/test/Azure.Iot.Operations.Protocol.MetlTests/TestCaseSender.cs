namespace Azure.Iot.Operations.Protocol.UnitTests.Protocol
{
    public class TestCaseSender
    {
        public static string? DefaultTelemetryName;
        public static string? DefaultTelemetryTopic;
        public static string? DefaultModelId;

        public string? TelemetryName { get; set; } = DefaultTelemetryName;

        public string? TelemetryTopic { get; set; } = DefaultTelemetryTopic;

        public string? ModelId { get; set; } = DefaultModelId;

        public string? TopicNamespace { get; set; }

        public Dictionary<string, string>? CustomTokenMap { get; set; }
    }
}
