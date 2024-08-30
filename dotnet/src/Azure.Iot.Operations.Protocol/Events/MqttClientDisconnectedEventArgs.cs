using System;
using System.Collections.Generic;
using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Protocol.Events
{
    public sealed class MqttClientDisconnectedEventArgs : EventArgs
    {
        public MqttClientDisconnectedEventArgs(
            bool clientWasConnected,
            MqttClientConnectResult? connectResult,
            MqttClientDisconnectReason reason,
            string? reasonString,
            List<MqttUserProperty>? userProperties,
            Exception? exception)
        {
            ClientWasConnected = clientWasConnected;
            ConnectResult = connectResult;
            Exception = exception;
            Reason = reason;
            ReasonString = reasonString;
            UserProperties = userProperties;
        }

        public bool ClientWasConnected { get; }

        /// <summary>
        ///     Gets the authentication result.
        ///     <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public MqttClientConnectResult? ConnectResult { get; }

        public Exception? Exception { get; }

        /// <summary>
        ///     Gets or sets the reason.
        ///     <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public MqttClientDisconnectReason Reason { get; }

        public string? ReasonString { get; }

        public List<MqttUserProperty>? UserProperties { get; }
    }
}
