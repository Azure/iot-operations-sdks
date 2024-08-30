namespace Azure.Iot.Operations.Protocol.UnitTests.Protocol
{
    public class DefaultPrologue
    {
        public DefaultPrologue()
        {
            Executor = new();
            Invoker = new();
        }

        public DefaultExecutor Executor { get; set; }

        public DefaultInvoker Invoker { get; set; }
    }
}
