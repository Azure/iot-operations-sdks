namespace Azure.Iot.Operations.Protocol.UnitTests.Protocol
{
    public class DefaultPrologue
    {
        public DefaultPrologue()
        {
            Executor = new();
            Invoker = new();
            Sender = new();
        }

        public DefaultExecutor Executor { get; set; }

        public DefaultInvoker Invoker { get; set; }

        public DefaultSender Sender { get; set; }
    }
}
