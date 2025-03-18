// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Azure.Iot.Operations.Protocol.Models
{
    public sealed class MqttClientDisconnectOptions
    {
        /// <summary>
        ///     Gets or sets the reason code.
        ///     <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public MqttClientDisconnectOptionsReason Reason { get; set; } = MqttClientDisconnectOptionsReason.NormalDisconnection;

        /// <summary>
        ///     Gets or sets the reason string.
        ///     <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public string? ReasonString { get; set; }

        /// <summary>
        ///     Gets or sets the session expiry interval.
        ///     <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public uint SessionExpiryInterval { get; set; }

        /// <summary>
        ///     Gets or sets the user properties.
        ///     <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public List<MqttUserProperty>? UserProperties { get; set; }

        public void AddUserProperty(string name, string value)
        {
            UserProperties ??= [];
            UserProperties.Add(new MqttUserProperty(name, value));
        }
    }
}
