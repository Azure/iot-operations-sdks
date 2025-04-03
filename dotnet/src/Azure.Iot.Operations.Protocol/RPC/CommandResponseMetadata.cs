﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using System;
using System.Collections.Generic;

namespace Azure.Iot.Operations.Protocol.RPC
{
    public class CommandResponseMetadata
    {
        // These two user properties are used to communicate application level errors in an RPC response message. Code is mandatory, but data is optional.
        public const string ApplicationErrorCodeUserDataKey = "AppErrCode";
        public const string ApplicationErrorPayloadUserDataKey = "AppErrPayload";

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
        public HybridLogicalClock? Timestamp { get; internal set; }

        /// <summary>
        /// The content type of a command response received by a command invoker if a content type was provided on the MQTT message.
        /// </summary>
        /// <remarks>
        /// This field is only set by the command invoker when deserializing a response. It cannot be used by a command executor to change the content type of a command response.
        /// </remarks>
        public string? ContentType { get; internal set; }

        /// <summary>
        /// The payload format indicator of a command response received by a command invoker.
        /// </summary>
        /// <remarks>
        /// This field is only set by the command invoker when deserializing a response. It cannot be used by a command executor to change the payload format indicator of a command response.
        /// </remarks>
        public MqttPayloadFormatIndicator PayloadFormatIndicator { get; internal set; }

        /// <summary>
        /// A dictionary of user properties that are sent along with the response from the CommandExecutor to the CommandInvoker.
        /// When CommandResponseMetadata is constructed within a user-code execution function on the CommandExecutor, the UserData is initialized with an empty dictionary.
        /// When CommandResponseMetadata is returned by command invocation on the CommandInvoker, the UserData is set from the response message.
        /// </summary>
        public Dictionary<string, string> UserData { get; set; } = new();

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

            Timestamp = null;
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
                message.AddUserProperty(kvp.Key, kvp.Value);
            }
        }

        public bool TryGetApplicationError<TError>(IErrorHeaderPayloadSerializer serializer, out ApplicationError<TError>? error) where TError : class
        {
            if (UserData == null || !UserData.TryGetValue(ApplicationErrorCodeUserDataKey, out string? errorCode))
            {
                error = null;
                return false;
            }

            error = new(errorCode);

            if (UserData.TryGetValue(ApplicationErrorPayloadUserDataKey, out string? errorPayloadString))
            {
                error.ErrorData = serializer.FromString<TError>(errorPayloadString);
            }

            return true;
        }

        public bool IsApplicationError()
        {
            return UserData != null && UserData.ContainsKey(ApplicationErrorCodeUserDataKey);
        }

        public void SetApplicationError<TError>(string applicationErrorCode, TError? errorData, IErrorHeaderPayloadSerializer? serializer) where TError : class
        {
            UserData ??= new();
            UserData[ApplicationErrorCodeUserDataKey] = applicationErrorCode;

            if (errorData != null && serializer == null)
            {
                throw new ArgumentNullException(nameof(serializer), "Must provide a serializer if non-null errorData is provided");
            }

            if (errorData != null && serializer != null)
            {
                try
                {
                    UserData[ApplicationErrorPayloadUserDataKey] = serializer.ToString<TError>(errorData);
                }
                catch (Exception)
                {
                    //TODO log or throw?
                }
            }
        }
    }
}
