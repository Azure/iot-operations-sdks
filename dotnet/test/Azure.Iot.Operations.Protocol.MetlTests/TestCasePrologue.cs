namespace Azure.Iot.Operations.Protocol.UnitTests.Protocol
{
    public class TestCasePrologue
    {
        public TestCaseMqttConfig? MqttConfig { get; set; }

        public TestCasePushAcks? PushAcks { get; set; }

        public List<TestCaseExecutor> Executors { get; set; } = new();

        public List<TestCaseInvoker> Invokers { get; set; } = new();

        public TestCaseCatch? Catch { get; set; }

        public Dictionary<string, int> CountdownEvents { get; set; } = new();
    }
}
