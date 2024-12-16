// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Protocol.Events
{
    public sealed class MqttClientConnectedEventArgs(MqttClientConnectResult connectResult) : EventArgs
    {

        /// <summary>
        ///     Gets the authentication result.
        ///     <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public MqttClientConnectResult ConnectResult { get; } = connectResult ?? throw new ArgumentNullException(nameof(connectResult));
    }
}
