namespace Azure.Iot.Operations.Protocol.UnitTests.Protocol
{
    public class TestCasePushAcks
    {
        public List<TestAckKind> Publish { get; set; } = new();

        public List<TestAckKind> Subscribe { get; set; } = new();

        public List<TestAckKind> Unsubscribe { get; set; } = new();
    }
}
