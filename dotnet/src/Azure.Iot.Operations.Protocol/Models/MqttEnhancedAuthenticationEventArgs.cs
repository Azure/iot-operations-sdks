// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System;

namespace Azure.Iot.Operations.Protocol.Models
{
    public class MqttEnhancedAuthenticationEventArgs : EventArgs
    {

        /// <summary>
        ///     Gets the authentication data.
        ///     Hint: MQTT 5 feature only.
        /// </summary>
        public byte[] AuthenticationData { get; set; }

        /// <summary>
        ///     Gets the authentication method.
        ///     Hint: MQTT 5 feature only.
        /// </summary>
        public string AuthenticationMethod { get; set; }

        /// <summary>
        ///     Gets the reason code.
        ///     Hint: MQTT 5 feature only.
        /// </summary>
        public MqttAuthenticateReasonCode ReasonCode { get; set; }

        /// <summary>
        ///     Gets the reason string.
        ///     Hint: MQTT 5 feature only.
        /// </summary>
        public string ReasonString { get; set; }

        /// <summary>
        ///     Gets the user properties.
        ///     In MQTT 5, user properties are basic UTF-8 string key-value pairs that you can append to almost every type of MQTT
        ///     packet.
        ///     As long as you don’t exceed the maximum message size, you can use an unlimited number of user properties to add
        ///     metadata to MQTT messages and pass information between publisher, broker, and subscriber.
        ///     The feature is very similar to the HTTP header concept.
        ///     Hint: MQTT 5 feature only.
        /// </summary>
        public List<MqttUserProperty> UserProperties { get; set; }

        public MqttEnhancedAuthenticationEventArgs(byte[] authenticationData, string authenticationMethod, MqttAuthenticateReasonCode reasonCode, string reasonString, List<MqttUserProperty> userProperties)
        {
            AuthenticationData = authenticationData;
            AuthenticationMethod = authenticationMethod;
            ReasonCode = reasonCode;
            ReasonString = reasonString;
            UserProperties = userProperties;
        }
    }
}
