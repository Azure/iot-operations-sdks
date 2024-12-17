namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class DefaultInvokeCommand
    {
        public string? CommandName { get; set; }

        public string? ExecutorId { get; set; }

        public string? RequestValue { get; set; }

        public TestCaseDuration? Timeout { get; set; }
    }
}
