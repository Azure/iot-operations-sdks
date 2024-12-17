// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using System;
using System.Collections.Generic;

namespace Azure.Iot.Operations.Protocol.RPC
{
    public class CommandRequestMetadata
    {
        /// <summary>
        /// The correlation data used to connect a command response to a command request.
        /// When CommandRequestMetadata is constructed by user code that will invoke a command, the CorrelationData is set to a new GUID.
        /// When CommandRequestMetadata is passed by a CommandExecutor into a user-code execution function, the Correlation Data is set from the request message.
        /// </summary>
        public Guid CorrelationId { get; }

        /// <summary>
        /// The MQTT Client ID of the Command invoker that sends the request.
        /// This property has no meaning to user code that will invoke a command; the InvokerClientId is set to null on construction.
        /// When CommandRequestMetadata is passed by a CommandExecutor into a user-code execution function, the InvokerClientId is set from the request message.
        /// </summary>
        public string? InvokerClientId { get; }

        /// <summary>
        /// The timestamp attached to the request.
        /// When CommandRequestMetadata is constructed by user code that will invoke a command, the Timestamp is set from the updated HybridLogicalClock of the CommandInvoker.
        /// When CommandRequestMetadata is passed by a CommandExecutor into a user-code execution function, the Timestamp is set from the request message; this will be null if the message contains no timestamp header.
        /// </summary>
        public HybridLogicalClock? Timestamp { get; }

        /// <summary>
        /// A fencing token attached to the request.
        /// When CommandRequestMetadata is constructed by user code that will invoke a command, the FencingToken is initialized to null, and it can be set by user code.
        /// When CommandRequestMetadata is passed by a CommandExecutor into a user-code execution function, the FencingToken is set from the request message; this will be null if the message contains no fencing token header.
        /// </summary>
        public HybridLogicalClock? FencingToken { get; set; }

        /// <summary>
        /// A dictionary of user properties that are sent along with the request from the CommandInvoker to the CommandExecutor.
        /// When CommandRequestMetadata is constructed by user code that will invoke a command, the UserData is initialized with an empty dictionary.
        /// When CommandRequestMetadata is passed by a CommandExecutor into a user-code execution function, the UserData is set from the request message.
        /// </summary>
        public Dictionary<string, string> UserData { get; }

        /// <summary>
        /// The partition attached to the request.
        /// When CommandRequestMetadata is constructed by user code that will invoke a command, the partition is initialized to null, and it can be set by user code.
        /// When CommandRequestMetadata is passed by a CommandExecutor into a user-code execution function, the partition is set from the request message; this will be null if the message contains no partition header.
        /// </summary>
        public string? Partition { get; }

        /// <summary>
        /// Construct CommandRequestMetadata in user code, for passing to a command invocation.
        /// </summary>
        /// <remarks>
        /// * The CorrelationData field will be set to a new GUID; if the CommandRequestMetadata is passed to a command invocation, this value will be used as the correlation date for the request.
        /// * The InvokerClientId field will be set to null; user code can obtain the MQTT client ID directly from the MQTT client.
        /// * The Timestamp field will be set to the current HybridLogicalClock time for the process.
        /// * The FencingToken field will be initialized to null; this can be set by user code if desired.
        /// * The UserData field will be initialized with an empty dictionary; entries in this dictionary can be set by user code as desired.
        /// </remarks>
        public CommandRequestMetadata()
        {
            HybridLogicalClock localClock = HybridLogicalClock.GetInstance();
            localClock.Update();

            CorrelationId = Guid.NewGuid();
            InvokerClientId = null;

            Timestamp = new HybridLogicalClock(localClock);
            FencingToken = null;
            UserData = [];
        }

        internal CommandRequestMetadata(MqttApplicationMessage message)
        {
            CorrelationId = message.CorrelationData != null && GuidExtensions.TryParseBytes(message.CorrelationData, out Guid? correlationId)
                ? correlationId!.Value
                : throw new ArgumentException($"Invalid property -- CorrelationData in request message is null or not parseable as a GUID", nameof(message));

            InvokerClientId = null;

            Timestamp = null;
            FencingToken = null;
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
                        case AkriSystemProperties.FencingToken:
                            FencingToken = HybridLogicalClock.DecodeFromString(AkriSystemProperties.FencingToken, property.Value);
                            break;
                        case AkriSystemProperties.SourceId:
                            InvokerClientId = property.Value;
                            break;
                        case "$partition":
                            Partition = property.Value;
                            break;
                        default:
                            if (!property.Name.StartsWith(AkriSystemProperties.ReservedPrefix, StringComparison.InvariantCulture))
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

            if (FencingToken != default)
            {
                message.AddUserProperty(AkriSystemProperties.FencingToken, FencingToken.EncodeToString());
            }

            if (Partition != null)
            {
                message.AddUserProperty("$partition", Partition);
            }

            foreach (KeyValuePair<string, string> kvp in UserData)
            {
                if (kvp.Key.StartsWith(AkriSystemProperties.ReservedPrefix, StringComparison.InvariantCulture))
                {
                    throw new AkriMqttException($"Invalid user property \"{kvp.Key}\" starts with reserved prefix {AkriSystemProperties.ReservedPrefix}")
                    {
                        Kind = AkriMqttErrorKind.HeaderInvalid,
                        InApplication = true,
                        IsShallow = false,
                        IsRemote = false,
                        HeaderName = kvp.Key,
                    };
                }

                message.AddUserProperty(kvp.Key, kvp.Value);
            }
        }
    }
}
