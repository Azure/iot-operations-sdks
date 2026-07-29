// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// Event arguments passed to <see cref="IManagementActionHandler.HandleAsync"/> when a
    /// management action is invoked. Contains the full request context so the handler can
    /// execute the appropriate device operation.
    /// </summary>
    /// <remarks>
    /// The action's <see cref="ActionType"/> is surfaced here (rather than via separate
    /// handler methods) because the SDK treats Call / Read / Write invocations symmetrically.
    /// Handlers that need to behave differently per type can branch on <see cref="ActionType"/>.
    /// </remarks>
    public class ManagementActionInvokedEventArgs : EventArgs
    {
        /// <summary>The management group name this action belongs to.</summary>
        public required string GroupName { get; init; }

        /// <summary>The management action name.</summary>
        public required string ActionName { get; init; }

        /// <summary>
        /// The action type (<c>Call</c>, <c>Read</c>, or <c>Write</c>) as declared on the
        /// management action definition. Fixed for the lifetime of the action — does not vary
        /// per request.
        /// </summary>
        public required AssetManagementGroupActionType ActionType { get; init; }

        /// <summary>Raw request payload bytes as delivered by the invoker.</summary>
        public required ReadOnlySequence<byte> Payload { get; init; }

        /// <summary>MIME type of <see cref="Payload"/>.</summary>
        public required string ContentType { get; init; }

        /// <summary>MQTT 5 payload format indicator reported by the invoker.</summary>
        public MqttPayloadFormatIndicator FormatIndicator { get; init; } = MqttPayloadFormatIndicator.Unspecified;

        /// <summary>MQTT 5 user properties sent by the invoker.</summary>
        public IReadOnlyDictionary<string, string> CustomUserData { get; init; }
            = new Dictionary<string, string>();

        /// <summary>HLC timestamp from the invoker, if present.</summary>
        public HybridLogicalClock? Timestamp { get; init; }

        /// <summary>Invoker identity as surfaced by the broker / auth policy, if any.</summary>
        public string? InvokerId { get; init; }

        /// <summary>
        /// Values extracted from wildcard segments of the action request topic pattern.
        /// </summary>
        public IReadOnlyDictionary<string, string> TopicTokens { get; init; }
            = new Dictionary<string, string>();

        /// <summary>The name of the asset that this action belongs to.</summary>
        public required string AssetName { get; init; }

        /// <summary>The name of the device that the asset belongs to.</summary>
        public required string DeviceName { get; init; }
    }
}

