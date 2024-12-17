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
        /// A method was called with an invalid argument value.
        /// </summary>
        ArgumentInvalid,

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
        /// The current program state is invalid vis-a-vis the method that was called.
        /// </summary>
        StateInvalid,

        /// <summary>
        /// The client or service observed a condition that was thought to be impossible.
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
        /// or this request specified a malformed protocol version.
        /// </summary>
        UnsupportedRequestVersion,

        /// <summary>
        /// The request failed because the command executor's response to this request specified a protocol
        /// version not supported by this command invoker or specified a malformed protocol version.
        /// </summary>
        UnsupportedResponseVersion,
    }
}
