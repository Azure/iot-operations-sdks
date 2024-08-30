namespace Azure.Iot.Operations.Protocol.UnitTests.Protocol
{
    public class TestCasePublishedMessage
    {
        public int? CorrelationIndex { get; set; }

        public string? Topic { get; set; }

        public object Payload { get; set; } = false;

        public Dictionary<string, string?> Metadata { get; set; } = new();

        public object CommandStatus { get; set; } = false;

        public bool? IsApplicationError { get; set; }

        public string? InvokerId { get; set; }
    }
}
