namespace Azure.Iot.Operations.Services.StateStore
{
    // <summary>
    // Enum representing the reason for a state store operation exception.
    // </summary>
    public enum StateStoreExceptionReason
    {
        /// <summary>
        /// The requested timestamp is too far in the future; ensure that the client and broker system clocks are synchronized.
        /// </summary>
        TimestampSkew,
        /// <summary>
        /// A fencing token is required for this request. This happens if a key has been marked with a fencing token, but the client doesn't specify it.
        /// </summary>
        MissingFencingToken,
        /// <summary>
        /// The requested fencing token timestamp is too far in the future; ensure that the client and broker system clocks are synchronized.
        /// </summary>
        FencingTokenSkew,
        /// <summary>
        /// The requested fencing token is a lower version that the fencing token protecting the resource.
        /// </summary>
        FencingTokenLowerVersion,
        /// <summary>
        /// The state store has a quota of how many keys it can store, which is based on the memory profile of the MQ broker that's specified.
        /// </summary>
        QuotaExceeded,
        /// <summary>
        /// The payload sent does not conform to state store's definition.
        /// </summary>
        SyntaxError,
        /// <summary>
        /// The client is not authorized to perform the operation.
        /// </summary>
        NotAuthorized,
        /// <summary>
        /// The command sent is not recognized by the state store.
        /// </summary>
        UnknownCommand,
        /// <summary>
        /// The number of arguments sent in the command is incorrect.
        /// </summary>
        WrongNumberOfArguments,
        /// <summary>
        /// The timestamp is missing on the request.
        /// </summary>
        TimestampMissing,
        /// <summary>
        /// The timestamp or fencing token is malformed.
        /// </summary>
        TimestampMalformed,
        /// <summary>
        /// The key length is zero.
        /// </summary>
        KeyLengthZero,
        /// <summary>
        /// An unknown error was received from the State Store Service.
        /// </summary>
        Unknown
    }

    public class StateStoreOperationException : Exception
    {
        public StateStoreExceptionReason Reason { get; }
        public StateStoreOperationException(string message, StateStoreExceptionReason reason, Exception innerException)
            : base(message, innerException)
        {
            Reason = reason;
        }

        public StateStoreOperationException(string message, StateStoreExceptionReason reason)
            : base(message)
        {
            Reason = reason;
        }

        public StateStoreOperationException(string message)
            : base(message)
        {
            Reason = StateStoreExceptionReason.Unknown;
        }
    }
}