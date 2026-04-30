// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// Event arguments passed to <see cref="IManagementActionHandler"/> methods when a
    /// management action is invoked. Contains the full request context so the handler can
    /// execute the appropriate device operation.
    /// </summary>
    public class ManagementActionInvokedEventArgs : EventArgs
    {
        /// <summary>The management group name this action belongs to.</summary>
        public required string GroupName { get; init; }

        /// <summary>The management action name.</summary>
        public required string ActionName { get; init; }

        /// <summary>The type of the management action (Call, Read, or Write).</summary>
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

