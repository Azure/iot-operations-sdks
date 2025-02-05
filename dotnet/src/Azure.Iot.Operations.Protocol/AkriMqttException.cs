// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Azure.Iot.Operations.Protocol
{
    public class AkriMqttException : Exception
    {
        public AkriMqttException(string message) : base(message)
        {
        }

        public AkriMqttException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// The specific kind of error that occurred
        /// </summary>
        public required AkriMqttErrorKind Kind { get; init; }

        /// <summary>
        /// <c>true</c> if the error occurred in user-supplied code rather than the SDK or its dependent components
        /// </summary>
        public required bool InApplication { get; init; }

        /// <summary>
        /// <c>true</c> if the error was identified immediately after the API was called, prior to any attempted network communication
        /// </summary>
        public required bool IsShallow { get; init; }

        /// <summary>
        /// <c>true</c> if the error was detected by a remote component
        /// </summary>
        public required bool IsRemote { get; init; }

        /// <summary>
        /// An HTTP status code received from a remote service that caused the Azure.Iot.Operations.Protocol error being reported
        /// </summary>
        public int? HttpStatusCode { get; internal init; }

        /// <summary>
        /// The correlation data used to connect a command response to a command request.
        /// </summary>
        public Guid? CorrelationId { get; internal init; }

        /// <summary>
        /// The name of an MQTT header that is missing or has an invalid value
        /// </summary>
        public string? HeaderName { get; init; }

        /// <summary>
        /// The value of an MQTT header that is invalid
        /// </summary>
        public string? HeaderValue { get; init; }

        /// <summary>
        /// The name of a timeout condition that elapsed
        /// </summary>
        public string? TimeoutName { get; internal init; }

        /// <summary>
        /// The duration of a timeout condition that elapsed
        /// </summary>
        public TimeSpan? TimeoutValue { get; internal init; }

        /// <summary>
        /// The name of a method argument or a property in a class, configuration file, or environment variable that is missing or has an invalid value
        /// </summary>
        public string? PropertyName { get; internal init; }

        /// <summary>
        /// The value of a method argument or a property in a class, configuration file, or environment variable that is invalid
        /// </summary>
        public object? PropertyValue { get; internal init; }

        /// <summary>
        /// The name of a command relevant to the Azure.Iot.Operations.Protocol error being reported
        /// </summary>
        public string? CommandName { get; internal init; }

        /// <summary>
        /// The protocol version that was not supported. Only provided if the error was either a 
        /// <see cref="AkriMqttErrorKind.UnsupportedRequestVersion"/> kind or a <see cref="AkriMqttErrorKind.UnsupportedResponseVersion"/> kind.
        /// </summary>
        public string? ProtocolVersion { get; set; }

        /// <summary>
        /// The list of supported protocol versions that can be used instead. Only provided if the error was either a 
        /// <see cref="AkriMqttErrorKind.UnsupportedRequestVersion"/> kind or a <see cref="AkriMqttErrorKind.UnsupportedResponseVersion"/> kind.
        /// </summary>
        public int[]? SupportedMajorProtocolVersions { get; internal set; }

        internal static AkriMqttException GetConfigurationInvalidException(
            string configurationName,
            object? configurationValue,
            string? message = default,
            Exception? innerException = default,
            string? commandName = default)
        {
            return innerException is null
                ? new AkriMqttException(message ?? $"invalid configuration value {configurationName} for configuration {configurationName}")
                {
                    Kind = AkriMqttErrorKind.ConfigurationInvalid,
                    InApplication = false,
                    IsShallow = true,
                    IsRemote = false,
                    PropertyName = configurationName,
                    PropertyValue = configurationValue,
                    CommandName = commandName,
                }
                : new AkriMqttException(message ?? $"invalid configuration value {configurationName} for configuration {configurationName}", innerException)
                {
                    Kind = AkriMqttErrorKind.ConfigurationInvalid,
                    InApplication = false,
                    IsShallow = true,
                    IsRemote = false,
                    PropertyName = configurationName,
                    PropertyValue = configurationValue,
                    CommandName = commandName,
                };
        }

        internal static AkriMqttException GetArgumentInvalidException(string? commandName, string argumentName, object? arguentValue, string? message = default)
        {
            string errMsg =
                message ?? (arguentValue != null ? $"argument {argumentName} has invalid value {arguentValue}" :
                $"argument {argumentName} has no value");

            return new AkriMqttException(errMsg)
            {
                Kind = AkriMqttErrorKind.ArgumentInvalid,
                InApplication = false,
                IsShallow = true,
                IsRemote = false,
                PropertyName = argumentName,
                PropertyValue = arguentValue,
                CommandName = commandName,
            };
        }

        public static AkriMqttException GetPayloadInvalidException()
        {
            return new AkriMqttException($"Command payload invalid")
            {
                Kind = AkriMqttErrorKind.PayloadInvalid,
                InApplication = false,
                IsShallow = false,
                IsRemote = false
            };
        }
    }
}
