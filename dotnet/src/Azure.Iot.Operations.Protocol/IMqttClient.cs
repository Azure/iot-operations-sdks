// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol
{
    /// <summary>
    /// An MQTT client interface that allows for connection management as well as publishing, subscribing, and 
    /// unsubscribing. 
    /// </summary>
    public interface IMqttClient : IMqttPubSubClient, IAsyncDisposable
    {
        /// <summary>
        /// An event that executes every time this client is disconnected.
        /// </summary>
        event Func<MqttClientDisconnectedEventArgs, Task>? DisconnectedAsync;

        /// <summary>
        /// An event that executes every time this client is connected.
        /// </summary>
        event Func<MqttClientConnectedEventArgs, Task>? ConnectedAsync;

        /// <summary>
        /// Connect this client to the MQTT broker configured in the provided connection options.
        /// </summary>
        /// <param name="options">The details about the MQTT broker to connect to.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The CONNACK returned by the MQTT broker.</returns>
        Task<MqttClientConnectResult> ConnectAsync(MqttClientOptions options, CancellationToken cancellationToken = default);

        /// <summary>
        /// Connect this client to the MQTT broker configured in the provided connection settings.
        /// </summary>
        /// <param name="settings">The details about the MQTT broker to connect to.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The CONNACK returned by the MQTT broker.</returns>
        Task<MqttClientConnectResult> ConnectAsync(MqttConnectionSettings settings, CancellationToken cancellationToken = default);

        /// <summary>
        /// Disconnect this client from the MQTT broker.
        /// </summary>
        /// <param name="options">The optional parameters to include in the DISCONNECT request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task DisconnectAsync(MqttClientDisconnectOptions? options = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reconnect the client if it is disconnected. This will use the <see cref="MqttClientOptions"/> last provided
        /// when connecting.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ReconnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get if this MQTT client is currently connected or not.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Send additional authentication data. May be done on an active connection.
        /// </summary>
        /// <param name="data">The authentication data to send.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SendEnhancedAuthenticationExchangeDataAsync(MqttEnhancedAuthenticationExchangeData data, CancellationToken cancellationToken = default);
    }
}
