// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using System;
using System.Collections.Generic;

namespace Azure.Iot.Operations.Protocol.RPC
{
    public class CommandResponseMetadata
    {
        /// <summary>
        /// The correlation data used to connect a command response to a command request.
        /// This property has no meaning to a user-code execution function on the CommandExecutor; the CorrelationData is set to null on construction.
        /// When CommandResponseMetadata is returned by command invocation on the CommandInvoker, the CorrelationData is set from the response message.
        /// </summary>
        public Guid? CorrelationId { get; }

        /// <summary>
        /// The timestamp attached to the response.
        /// When CommandResponseMetadata is constructed within a user-code execution function on the CommandExecutor, the Timestamp is set from the HybridLogicalClock of the CommandExecutor.
        /// When CommandResponseMetadata is returned by command invocation on the CommandInvoker, the Timestamp is set from the response message; this will be null if the message contains no timestamp header.
        /// </summary>
        public HybridLogicalClock? Timestamp { get; }

        /// <summary>
        /// A dictionary of user properties that are sent along with the response from the CommandExecutor to the CommandInvoker.
        /// When CommandResponseMetadata is constructed within a user-code execution function on the CommandExecutor, the UserData is initialized with an empty dictionary.
        /// When CommandResponseMetadata is returned by command invocation on the CommandInvoker, the UserData is set from the resonse message.
        /// </summary>
        public Dictionary<string, string> UserData { get; set; } = [];

        /// <summary>
        /// Construct CommandResponseMetadata in user code, presumably within an execution function that will include the metadata in its return value.
        /// </summary>
        /// <remarks>
        /// * The CorrelationData field will be set to null; if the user-code execution function wants to know the correlation data, it should use the CommandRequestMetadata passed in by the CommandExecutor.
        /// * The Status field will be set to null; the command status will not be determined until after execution completes.
        /// * The StatusMessage field will be set to null; the command status will not be determined until after execution completes.
        /// * The IsApplicationError field will be set to null; the command status will not be determined until after execution completes.
        /// * The Timestamp field will be set to the current HybridLogicalClock time for the process.
        /// * The UserData field will be initialized with an empty dictionary; entries in this dictionary can be set by user code as desired.
        /// </remarks>
        public CommandResponseMetadata()
        {
            CorrelationId = null;

            Timestamp = new HybridLogicalClock(HybridLogicalClock.GetInstance());
            UserData = [];
        }

        internal CommandResponseMetadata(MqttApplicationMessage message)
        {
            CorrelationId = message.CorrelationData != null && GuidExtensions.TryParseBytes(message.CorrelationData, out Guid? correlationId)
                ? (Guid?)correlationId!.Value
                : throw new ArgumentException($"Invalid property -- CorrelationData in response message is null or not parseable as a GUID", nameof(message));

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
                            if (!property.Name.StartsWith(AkriSystemProperties.ReservedPrefix, StringComparison.InvariantCulture))
                            {
                                UserData[property.Name] = property.Value;
                            }
                            break;
                    }
                }
            }
        }

        public void MarshalTo(MqttApplicationMessage message)
        {
            if (Timestamp != default)
            {
                message.AddUserProperty(AkriSystemProperties.Timestamp, Timestamp.EncodeToString());
            }

            foreach (KeyValuePair<string, string> kvp in UserData)
            {
                if (kvp.Key.StartsWith(AkriSystemProperties.ReservedPrefix, StringComparison.InvariantCulture))
                {
                    throw new AkriMqttException($"Invalid user property \"{kvp.Key}\" starts with reserved prefix {AkriSystemProperties.ReservedPrefix}")
                    {
                        Kind = AkriMqttErrorKind.ExecutionException,
                        InApplication = true,
                        IsShallow = false,
                        IsRemote = false,
                        PropertyName = "Metadata",
                        PropertyValue = kvp.Key,
                    };
                }

                message.AddUserProperty(kvp.Key, kvp.Value);
            }
        }
    }
}
