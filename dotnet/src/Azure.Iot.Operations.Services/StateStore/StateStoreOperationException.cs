namespace Azure.Iot.Operations.Services.StateStore
{
    public class StateStoreOperationException : Exception
    {
        public StateStoreOperationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public StateStoreOperationException(string message)
            : base(message)
        {
        }
    }
}