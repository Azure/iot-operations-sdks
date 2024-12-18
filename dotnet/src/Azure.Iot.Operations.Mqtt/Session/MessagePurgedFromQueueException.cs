// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using System;

namespace Azure.Iot.Operations.Mqtt.Session.Exceptions
{
    /// <summary>
    /// Thrown by a <see cref="MqttSessionClient.PublishAsync(MQTTnet.MqttApplicationMessage, CancellationToken)"/> if the message is 
    /// removed from the queue because the message queue size was reached. Depending on the <see cref="MqttSessionClientOptions.PendingMessagesOverflowStrategy"/>,
    /// this either signals that this message was the first message in the queue when the max queue size was reached or
    /// that this message tried to be enqueued when the queue was already at the max queue size.
    /// </summary>
    public class MessagePurgedFromQueueException : Exception
    {
        public MqttPendingMessagesOverflowStrategy? MessagePurgeStrategy { get; }

        public MessagePurgedFromQueueException(MqttPendingMessagesOverflowStrategy? messagePurgeStrategy = null)
        {
            MessagePurgeStrategy = messagePurgeStrategy;
        }

        public MessagePurgedFromQueueException(MqttPendingMessagesOverflowStrategy? messagePurgeStrategy, string? message) : base(message)
        {
            MessagePurgeStrategy = messagePurgeStrategy;
        }

        public MessagePurgedFromQueueException(MqttPendingMessagesOverflowStrategy? messagePurgeStrategy, string? message, Exception? innerException) : base(message, innerException)
        {
            MessagePurgeStrategy = messagePurgeStrategy;
        }
    }
}
