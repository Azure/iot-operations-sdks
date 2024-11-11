namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class TestCaseSender
    {
        public static string? DefaultTelemetryName;
        public static string? DefaultTelemetryTopic;
        public static string? DefaultModelId;
        public static string? DefaultTopicNamespace;

        public string? TelemetryName { get; set; } = DefaultTelemetryName;

        public string? TelemetryTopic { get; set; } = DefaultTelemetryTopic;

        public string? ModelId { get; set; } = DefaultModelId;

        public string? TopicNamespace { get; set; } = DefaultTopicNamespace;

        public Dictionary<string, string>? CustomTokenMap { get; set; }
    }
}
