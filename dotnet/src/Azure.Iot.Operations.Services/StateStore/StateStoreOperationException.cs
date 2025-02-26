// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
        // A fencing token is required for this request.
        // </summary>
        // <remarks>
        // This error code should not happen when using this library as it will always format requests correctly.
        // </remarks>
        MissingFencingToken,
        // <summary>
        // The requested fencing token timestamp is too far in the future; ensure that the client and broker system clocks are synchronized.
        // </summary>
        FencingTokenSkew,
        // <summary>
        // The requested fencing token is a lower version than the fencing token protecting the resource.
        // </summary>
        FencingTokenLowerVersion,
        // <summary>
        // The quota has been exceeded.
        // </summary>
        KeyQuotaExceeded,
        // <summary>
        // Syntax error.
        // </summary>
        // <remarks>
        // This error code should not happen when using this library as it will always format requests correctly.
        // </remarks>
        SyntaxError,
        // <summary>
        // Not authorized.
        // </summary>
        NotAuthorized,
        // <summary>
        // Unknown command.
        // </summary>
        // <remarks>
        // This error code should not happen when using this library as it will always format requests correctly.
        // </remarks>
        UnknownCommand,
        // <summary>
        // Wrong number of arguments.
        // </summary>
        // <remarks>
        // This error code should not happen when using this library as it will always format requests correctly.
        // </remarks>
        WrongNumberOfArguments,
        // <summary>
        // Missing timestamp.
        // </summary>
        // <remarks>
        // This error code should not happen when using this library as it will always format requests correctly.
        // </remarks>
        TimestampMissing,
        // <summary>
        // Malformed timestamp.
        // </summary>
        // <remarks>
        // This error code should not happen when using this library as it will always format requests correctly.
        // </remarks>
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
            { "the quota has been exceeded", ServiceError.KeyQuotaExceeded },
            { "syntax error", ServiceError.SyntaxError },
            { "not authorized", ServiceError.NotAuthorized },
            { "unknown command", ServiceError.UnknownCommand },
            { "wrong number of arguments", ServiceError.WrongNumberOfArguments },
            { "missing timestamp", ServiceError.TimestampMissing },
            { "malformed timestamp", ServiceError.TimestampMalformed },
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