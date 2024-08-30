namespace Azure.Iot.Operations.Services.StateStore.RESP3
{
    internal class Resp3ArrayBuilder
    {
        private List<byte[]> _resp3Objects;

        internal Resp3ArrayBuilder()
        {
            _resp3Objects = new List<byte[]>();
        }

        internal Resp3ArrayBuilder Add(byte[] resp3Object)
        {
            _resp3Objects.Add(resp3Object);
            return this;
        }

        internal byte[] Build()
        {
            return Resp3Protocol.BuildArray(_resp3Objects.ToArray());
        }
    }
}