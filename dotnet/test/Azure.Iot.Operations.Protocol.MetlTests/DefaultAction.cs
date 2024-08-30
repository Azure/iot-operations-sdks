namespace Azure.Iot.Operations.Protocol.UnitTests.Protocol
{
    public class DefaultAction
    {
        public DefaultAction()
        {
            InvokeCommand = new();
            ReceiveRequest = new();
            ReceiveResponse = new();
        }

        public DefaultInvokeCommand InvokeCommand { get; set; }

        public DefaultReceiveRequest ReceiveRequest { get; set; }

        public DefaultReceiveResponse ReceiveResponse { get; set; }
    }
}
