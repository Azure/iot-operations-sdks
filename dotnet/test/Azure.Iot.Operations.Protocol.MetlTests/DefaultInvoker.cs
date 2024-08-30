namespace Azure.Iot.Operations.Protocol.UnitTests.Protocol
{
    public class DefaultInvoker
    {
        public string? CommandName { get; set; }

        public string? RequestTopic { get; set; }

        public string? ModelId { get; set; }

        public string? ResponseTopicPrefix { get; set; }

        public string? ResponseTopicSuffix { get; set; }
    }
}
