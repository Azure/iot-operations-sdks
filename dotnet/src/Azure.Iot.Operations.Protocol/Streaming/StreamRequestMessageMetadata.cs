// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Protocol.Streaming
{
    /// <summary>
    /// Metadata for a specific message within a request stream
    /// </summary>
    public class StreamRequestMessageMetadata
    {
        public HybridLogicalClock? Timestamp { get; internal set; }

        public Dictionary<string, string> UserData { get; } = new();

        public StreamRequestMessageMetadata()
        {
        }

        internal StreamRequestMessageMetadata(MqttApplicationMessage message)
        {
            Timestamp = null;
            UserData = [];

            if (message.UserProperties != null)
            {
                foreach (MqttUserProperty property in message.UserProperties)
                {
                    switch (property.Name)
                    {
                        case AkriSystemProperties.Timestamp:
                            Timestamp = HybridLogicalClock.DecodeFromString(AkriSystemProperties.Timestamp, property.Value);
                            break;
                        default:
                            if (!AkriSystemProperties.IsReservedUserProperty(property.Name))
                            {
                                UserData[property.Name] = property.Value;
                            }
                            break;
                    }
                }
            }
        }

        internal void MarshalTo(MqttApplicationMessage message)
        {
            if (Timestamp != default)
            {
                message.AddUserProperty(AkriSystemProperties.Timestamp, Timestamp.EncodeToString());
            }

            foreach (KeyValuePair<string, string> kvp in UserData)
            {
                message.AddUserProperty(kvp.Key, kvp.Value);
            }
        }
    }
}
