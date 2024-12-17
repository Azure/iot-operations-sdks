namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class TestCaseActionInvokeCommand : TestCaseAction
    {
        public static string? DefaultCommandName;
        public static string? DefaultExecutorId;
        public static string? DefaultRequestValue;
        public static TestCaseDuration? DefaultTimeout;

        public int? InvocationIndex { get; set; }

        public string? CommandName { get; set; } = DefaultCommandName;

        public string? ExecutorId { get; set; } = DefaultExecutorId;

        public TestCaseDuration? Timeout { get; set; } = DefaultTimeout;

        public string? RequestValue { get; set; } = DefaultRequestValue;

        public Dictionary<string, string>? Metadata { get; set; }
    }
}
