namespace Azure.Iot.Operations.Protocol.UnitTests.Protocol
{
    public class DefaultTestCase
    {
        public DefaultTestCase()
        {
            Prologue = new();
            Actions = new();
        }

        public DefaultPrologue Prologue { get; set; }

        public DefaultAction Actions { get; set; }
    }
}
