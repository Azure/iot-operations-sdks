namespace Azure.Iot.Operations.Services.StateStore
{
    public class StateStoreValue : StateStoreObject
    {
        public StateStoreValue(string value) : base(value)
        {
        }

        public StateStoreValue(byte[] value) : base(value)
        {
        }

        public StateStoreValue(Stream value) : base(value)
        {
        }

        public StateStoreValue(Stream value, long length) : base(value, length)
        {
        }

        public StateStoreValue(ArraySegment<byte> value) : base(value)
        {
        }

        public StateStoreValue(IEnumerable<byte> value) : base (value)
        {
        }

        public static implicit operator StateStoreValue(string value)
        {
            if (value == null || value.Length == 0)
            {
                return new StateStoreValue(string.Empty);
            }

            return new StateStoreValue(value);
        }
    }
}