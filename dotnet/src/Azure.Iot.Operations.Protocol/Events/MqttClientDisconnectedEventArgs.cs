// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Protocol.Events
{
    public sealed class MqttClientDisconnectedEventArgs(
        bool clientWasConnected,
        MqttClientConnectResult? connectResult,
        MqttClientDisconnectReason reason,
        string? reasonString,
        List<MqttUserProperty>? userProperties,
        Exception? exception) : EventArgs
    {
        public bool ClientWasConnected { get; } = clientWasConnected;

        /// <summary>
        ///     Gets the authentication result.
        ///     <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public MqttClientConnectResult? ConnectResult { get; } = connectResult;

        public Exception? Exception { get; } = exception;

        /// <summary>
        ///     Gets or sets the reason.
        ///     <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public MqttClientDisconnectReason Reason { get; } = reason;

        public string? ReasonString { get; } = reasonString;

        public List<MqttUserProperty>? UserProperties { get; } = userProperties;
    }
}
