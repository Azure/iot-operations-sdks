// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.Retry;

namespace Azure.Iot.Operations.Mqtt.Session
{
    /// <summary>
    /// The optional parameters that can be specified when creating a session client.
    /// </summary>
    public class MqttSessionClientOptions
    {
        /// <summary>
        /// The maximum number of publishes, subscribes, or unsubscribes that will be allowed to be enqueued locally at a time.
        /// </summary>
        /// <remarks>
        /// Publishes, subscribes and unsubscribes all occupy separate queues, so this max value is for each of those queues..
        /// </remarks>
        public uint MaxPendingMessages { get; set; } = uint.MaxValue;

        /// <summary>
        /// The strategy for the session client to use when deciding how to handle enqueueing a message when the queue is already full.
        /// </summary>
        public MqttPendingMessagesOverflowStrategy PendingMessagesOverflowStrategy { get; set; } = MqttPendingMessagesOverflowStrategy.DropNewMessage;

        /// <summary>
        /// The retry policy that the session client will consult each time it attempts to reconnect and/or each time it attempts the initial connect.
        /// </summary>
        /// <remarks>
        /// By default, this is an <see cref="ExponentialBackoffRetryPolicy"/> that runs for around 4 minutes. Users may implement custom retry policies
        /// instead if they prefer to use a different retry algorithm.
        /// 
        /// This value cannot be null.
        /// </remarks>
        public IRetryPolicy ConnectionRetryPolicy { get; set; } = new ExponentialBackoffRetryPolicy(12, TimeSpan.MaxValue);

        /// <summary>
        /// True if you want the session client to enable MQTT-level logs. False if you do not want these logs.
        /// </summary>
        public bool EnableMqttLogging { get; set; }

        /// <summary>
        /// If true, this client will use the same retry policy when first connecting as it would during a reconnection.
        /// If false, this client will only make one attempt to connect when calling <see cref="MqttSessionClient.ConnectAsync(MQTTnet.MqttClientOptions, CancellationToken)"/>.
        /// </summary>
        /// <remarks>
        /// Generally, this field should be set to true since you can expect mostly the same set of errors when initially connecting 
        /// compared to when reconnecting. However, there are some exceptions that you are likely to see when initially connecting
        /// if you have a misconfiguration somewhere. This value is false by default so that these configuration errors are easier
        /// to catch.
        /// </remarks>
        public bool RetryOnFirstConnect { get; set; }

        /// <summary>
        /// How long to wait for a single connection attempt to finish before abandoning it.
        /// </summary>
        /// <remarks>
        /// This value allows for you to configure the connection attempt timeout for both initial
        /// connection and reconnection scenarios. Note that this value is ignored for the initial 
        /// connect attempt if <see cref="RetryOnFirstConnect"/> is false.
        /// </remarks>
        public TimeSpan ConnectionAttemptTimeout { get; set; } = TimeSpan.FromSeconds(2);

        internal void Validate()
        {
            if (MaxPendingMessages < 1)
            {
                throw new NotSupportedException("Max pending message count must be greater than 0");
            }

            if (PendingMessagesOverflowStrategy != MqttPendingMessagesOverflowStrategy.DropOldestQueuedMessage
                && PendingMessagesOverflowStrategy != MqttPendingMessagesOverflowStrategy.DropNewMessage)
            {
                throw new NotSupportedException("Pending messages overflow strategy must be \"DropOldestQueuedMessage\" or \"DropNewMessage\"");
            }

            ArgumentNullException.ThrowIfNull(ConnectionRetryPolicy, "A session client must have a retry policy.");

            if (ConnectionRetryPolicy is NoRetryPolicy)
            {
                throw new ArgumentException("A session client cannot use a 'NoRetry' policy");
            }
        }
    }
}
