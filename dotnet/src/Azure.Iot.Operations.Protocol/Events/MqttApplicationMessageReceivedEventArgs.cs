// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol.Events
{
    public sealed class MqttApplicationMessageReceivedEventArgs(
        string clientId,
        MqttApplicationMessage applicationMessage,
        ushort packetId,
        Func<MqttApplicationMessageReceivedEventArgs, CancellationToken, Task> acknowledgeHandler) : EventArgs
    {
        internal Func<MqttApplicationMessageReceivedEventArgs, CancellationToken, Task> AcknowledgeHandler { get; set; } = acknowledgeHandler;

        private int _isAcknowledged;

        public MqttApplicationMessage ApplicationMessage { get; } = applicationMessage ?? throw new ArgumentNullException(nameof(applicationMessage));

        /// <summary>
        ///     Gets or sets whether the library should send MQTT ACK packets automatically if required.
        /// </summary>
        public bool AutoAcknowledge { get; set; } = true;

        /// <summary>
        ///     Gets the client identifier.
        ///     Hint: This identifier needs to be unique over all used clients / devices on the broker to avoid connection issues.
        /// </summary>
        public string ClientId { get; } = clientId;

        /// <summary>
        ///     Gets or sets whether this message was handled.
        ///     This value can be used in user code for custom control flow.
        /// </summary>
        public bool IsHandled { get; set; }

        /// <summary>
        ///     Gets the identifier of the MQTT packet
        /// </summary>
        public ushort PacketIdentifier { get; set; } = packetId;

        /// <summary>
        ///     Gets or sets the reason code which will be sent to the server.
        /// </summary>
        public MqttApplicationMessageReceivedReasonCode ReasonCode { get; set; } = MqttApplicationMessageReceivedReasonCode.Success;

        /// <summary>
        ///     Gets or sets the reason string which will be sent to the server in the ACK packet.
        /// </summary>
        public string? ResponseReasonString { get; set; }

        /// <summary>
        ///     Gets or sets the user properties which will be sent to the server in the ACK packet etc.
        /// </summary>
        public List<MqttUserProperty> ResponseUserProperties { get; } = [];

        public object? Tag { get; set; }

        public Task AcknowledgeAsync(CancellationToken cancellationToken)
        {
            return Interlocked.CompareExchange(ref _isAcknowledged, 1, 0) == 0
                ? AcknowledgeHandler(this, cancellationToken)
                : throw new InvalidOperationException("The application message is already acknowledged.");
        }
    }
}