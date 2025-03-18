// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol
{
    public enum AkriMqttErrorKind
    {
        /// <summary>
        /// A class property, configuration file, or environment variable has an invalid value.
        /// </summary>
        ConfigurationInvalid,

        /// <summary>
        /// A required MQTT header property is missing.
        /// </summary>
        HeaderMissing,

        /// <summary>
        /// An MQTT header property is has an invalid value.
        /// </summary>
        HeaderInvalid,

        /// <summary>
        /// MQTT paylod cannot be deserialized.
        /// </summary>
        PayloadInvalid,

        /// <summary>
        /// There is some unexpected or incorrect state in the application layer code or in the operating system.
        /// </summary>
        StateInvalid,

        /// <summary>
        /// The client or service observed a condition that was thought to be impossible. This should indicate an issue with this SDK rather than an issue with application code.
        /// </summary>
        InternalLogicError,

        /// <summary>
        /// An operation was aborted due to timeout.
        /// </summary>
        Timeout,

        /// <summary>
        /// An operation was canceled.
        /// </summary>
        Cancellation,

        /// <summary>
        /// The command processor identified an error in the request.
        /// </summary>
        InvocationException,

        /// <summary>
        /// The command processor encountered an error while executing the command.
        /// </summary>
        ExecutionException,

        /// <summary>
        /// The client or service caught an unexpected error from a dependent component.
        /// </summary>
        UnknownError,

        /// <summary>
        /// The MQTT communication encountered an error and failed. The exception message should be inspected for additional information.
        /// </summary>
        MqttError,

        /// <summary>
        /// The request failed because the command executor did not support the protocol version specified by this request
        /// or this request specified a malformed protocol version. Alternatively, the request failed because the command executor's
        /// response to this request specified a protocol version not supported by this command invoker or specified a malformed protocol version.
        /// </summary>
        UnsupportedVersion,
    }
}
