namespace Azure.Iot.Operations.Services.StateStore
{
    // <summary>
    // Enum representing the reason for a state store implementation exception.
    // </summary>
    public enum StateStoreErrorKind
    {
        // <summary>
        // Error occurred in the AIO protocol
        // </summary>
        AIOProtocolError,
        // <summary>
        // Error occurred from the State Store Service.
        // </summary>
        ServiceError,
        // <summary>
        // Key length must not be zero.
        // </summary>
        KeyLengthZero,
        // <summary>
        // Error occurred during serialization of a request.
        // </summary>
        SerializationError,
        // <summary>
        // Argument provided for a request was invalid.
        // </summary>
        InvalidArgument,
        // <summary>
        // Payload of the response does not match the expected type for the request.
        // </summary>
        UnexpectedPayload
    }

    // <summary>
    // Enum representing the reason for a state store service exception.
    // </summary>
    public enum ServiceError
    {
        // <summary>
        // The requested timestamp is too far in the future; ensure that the client and broker system clocks are synchronized.
        // </summary>
        TimestampSkew,
        // <summary>
        // A fencing token is required for this request. This happens if a key has been marked with a fencing token, but the client doesn't specify it.
        // </summary>
        MissingFencingToken,
        // <summary>
        // The requested fencing token timestamp is too far in the future; ensure that the client and broker system clocks are synchronized.
        // </summary>
        FencingTokenSkew,
        // <summary>
        // The requested fencing token is a lower version that the fencing token protecting the resource.
        // </summary>
        FencingTokenLowerVersion,
        // <summary>
        // The state store has a quota of how many keys it can store, which is based on the memory profile of the MQ broker that's specified.
        // </summary>
        QuotaExceeded,
        // <summary>
        // The payload sent does not conform to state store's definition.
        // </summary>
        SyntaxError,
        // <summary>
        // The client is not authorized to perform the operation.
        // </summary>
        NotAuthorized,
        // <summary>
        // The command sent is not recognized by the state store.
        // </summary>
        UnknownCommand,
        // <summary>
        // The number of arguments sent in the command is incorrect.
        // </summary>
        WrongNumberOfArguments,
        // <summary>
        // The timestamp is missing on the request.
        // </summary>
        TimestampMissing,
        // <summary>
        // The timestamp or fencing token is malformed.
        // </summary>
        TimestampMalformed,
        // <summary>
        // The key length is zero.
        // </summary>
        KeyLengthZero,
        // <summary>
        // An unknown error was received from the State Store Service.
        // </summary>
        Unknown
    }

    public class StateStoreOperationException : Exception
    {
        public ServiceError Reason { get; }

        private static readonly Dictionary<string, ServiceError> ErrorMessages = new Dictionary<string, ServiceError>
        {
            { "the request timestamp is too far in the future; ensure that the client and broker system clocks are synchronized", ServiceError.TimestampSkew },
            { "a fencing token is required for this request", ServiceError.MissingFencingToken },
            { "the request fencing token timestamp is too far in the future; ensure that the client and broker system clocks are synchronized", ServiceError.FencingTokenSkew },
            { "the request fencing token is a lower version than the fencing token protecting the resource", ServiceError.FencingTokenLowerVersion },
            { "the state store has a quota of how many keys it can store, which is based on the memory profile of the MQ broker that's specified", ServiceError.QuotaExceeded },
            { "the payload sent does not conform to state store's definition", ServiceError.SyntaxError },
            { "the client is not authorized to perform the operation", ServiceError.NotAuthorized },
            { "the command sent is not recognized by the state store", ServiceError.UnknownCommand },
            { "the number of arguments sent in the command is incorrect", ServiceError.WrongNumberOfArguments },
            { "the timestamp is missing on the request", ServiceError.TimestampMissing },
            { "the timestamp or fencing token is malformed", ServiceError.TimestampMalformed },
            { "the key length is zero", ServiceError.KeyLengthZero }
        };

        public StateStoreOperationException(string message, Exception innerException, ServiceError reason)
            : base(message, innerException)
        {
            Reason = reason;
        }

        public StateStoreOperationException(string message, Exception innerException)
            : base(FormatMessage(message, ReasonFromMessage(message).Item1), innerException)
        {
            Reason = ReasonFromMessage(message).Item1;
        }

        public StateStoreOperationException(string message)
            : base(FormatMessage(message, ReasonFromMessage(message).Item1))
        {
            Reason = ReasonFromMessage(message).Item1;
        }

        private static (ServiceError, string) ReasonFromMessage(string message)
        {
            foreach (var errorMessage in ErrorMessages)
            {
                if (message.Contains(errorMessage.Key))
                {
                    return (errorMessage.Value, message);
                }
            }
            return (ServiceError.Unknown, message);
        }

        private static string FormatMessage(string originalMessage, ServiceError reason)
        {
            return reason == ServiceError.Unknown ? $"Unknown error: {originalMessage}" : originalMessage;
        }
    }
}