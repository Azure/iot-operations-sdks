namespace Azure.Iot.Operations.Protocol.UnitTests.Protocol
{
    public class TestCaseActionAwaitInvocation : TestCaseAction
    {
        public int? InvocationIndex { get; set; }

        public object ResponseValue { get; set; } = false;

        public Dictionary<string, string>? Metadata { get; set; }

        public TestCaseCatch? Catch { get; set; }
    }
}
