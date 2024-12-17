// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol
{
    /// <summary>
    /// An MQTT client interface that is intentionally limited to publish and subscribe operations. Implementations
    /// of this interface may provide connect/disconnect functions and may or may not include retry logic to handle
    /// when a publish or subscribe is attempted when disconnected.
    /// </summary>
    public interface IMqttPubSubClient : IAsyncDisposable
    {
        /// <summary>
        /// The event that notifies you when this client receives a PUBLISH from the MQTT broker.
        /// </summary>
        /// <remarks>
        /// Users are responsible for acknowledging each received PUBLISH. Users must either automatically acknowledge the message
        /// by opting into automatically sending the acknowledgement by setting the <see cref="MqttApplicationMessageReceivedEventArgs.AutoAcknowledge"/> flag, 
        /// or they can manually acknowledge the PUBLISH by invoking <see cref="MqttApplicationMessageReceivedEventArgs.AcknowledgeAsync(CancellationToken)"/>.
        /// 
        /// Note that this client sends PUBLISH acknowledgements in the order that the corresponding PUBLISH packets were received,
        /// so failing to acknowledge a PUBLISH will block sending acknowledgements for all subsequent PUBLISH packets received.
        /// </remarks>
        event Func<MqttApplicationMessageReceivedEventArgs, Task>? ApplicationMessageReceivedAsync;

        /// <summary>
        /// Publish a message to the MQTT broker.
        /// </summary>
        /// <param name="applicationMessage">The message to publish</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The result of the publish.</returns>
        /// <remarks>
        /// The behavior of publishing when the MQTT client is disconnected will vary depending on the implementation.
        /// </remarks>
        Task<MqttClientPublishResult> PublishAsync(MqttApplicationMessage applicationMessage, CancellationToken cancellationToken = default);

        /// <summary>
        /// Subscribe to a topic on the MQTT broker.
        /// </summary>
        /// <param name="options">The details of the subscribe.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The MQTT broker's response.</returns>
        /// <remarks>
        /// The behavior of subscribing when the MQTT client is disconnected will vary depending on the implementation.
        /// </remarks>
        Task<MqttClientSubscribeResult> SubscribeAsync(MqttClientSubscribeOptions options, CancellationToken cancellationToken = default);

        /// <summary>
        /// Unsubscribe from a topic on the MQTT broker.
        /// </summary>
        /// <param name="options">The details of the unsubscribe request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The MQTT broker's response.</returns>
        /// <remarks>
        /// The behavior of unsubscribing when the MQTT client is disconnected will vary depending on the implementation.
        /// </remarks>
        Task<MqttClientUnsubscribeResult> UnsubscribeAsync(MqttClientUnsubscribeOptions options, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the client Id used by this client.
        /// </summary>
        /// <remarks>
        /// If a client Id has not been assigned yet by the user or by the broker, this value is null.
        /// </remarks>
        string? ClientId { get; }

        /// <summary>
        /// The version of the MQTT protocol that this client is using.
        /// </summary>
        MqttProtocolVersion ProtocolVersion { get; }

        ValueTask DisposeAsync(bool disposing);
    }
}
