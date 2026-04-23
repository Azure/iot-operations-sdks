// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// An incoming management action invocation. Produced by
    /// <see cref="ManagementActionExecutor.RecvRequestAsync(System.Threading.CancellationToken)"/>.
    /// The connector must call <see cref="CompleteAsync(ManagementActionResponse, System.Threading.CancellationToken)"/>
    /// exactly once per request. If the request is disposed without being completed,
    /// an error response is sent back to the invoker automatically.
    /// </summary>
    public sealed class ManagementActionRequest : IAsyncDisposable
    {
        internal ManagementActionRequest()
        {
            // Populated by ManagementActionExecutor from the underlying ExtendedRequest.
        }

        /// <summary>Raw request payload bytes as delivered by the invoker.</summary>
        public byte[] Payload { get; internal set; } = Array.Empty<byte>();

        /// <summary>MIME type of <see cref="Payload"/>.</summary>
        public string ContentType { get; internal set; } = string.Empty;

        /// <summary>MQTT 5 payload format indicator reported by the invoker.</summary>
        public MqttPayloadFormatIndicator FormatIndicator { get; internal set; } = MqttPayloadFormatIndicator.Unspecified;

        /// <summary>MQTT 5 user properties sent by the invoker.</summary>
        public IReadOnlyDictionary<string, string> CustomUserData { get; internal set; }
            = new Dictionary<string, string>();

        /// <summary>HLC timestamp from the invoker, if present.</summary>
        public HybridLogicalClock? Timestamp { get; internal set; }

        /// <summary>Invoker identity as surfaced by the broker / auth policy, if any.</summary>
        public string? InvokerId { get; internal set; }

        /// <summary>
        /// Values extracted from wildcard segments of the action request topic pattern
        /// (e.g. <c>{deviceName}</c> → actual device name).
        /// </summary>
        public IReadOnlyDictionary<string, string> TopicTokens { get; internal set; }
            = new Dictionary<string, string>();

        /// <summary>
        /// True if the executor has been cancelled (action deleted, connector shutting
        /// down, etc.) after this request was received. A completion call will still be
        /// attempted but may not reach the invoker.
        /// </summary>
        public bool IsCancelled => throw new NotImplementedException();

        /// <summary>
        /// Complete the request with <paramref name="response"/>. Must be called at most
        /// once; subsequent calls throw.
        /// </summary>
        public Task CompleteAsync(ManagementActionResponse response, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        /// <summary>
        /// Releases the request. If <see cref="CompleteAsync"/> was not called, an
        /// application-error response is sent back to the invoker automatically.
        /// </summary>
        public ValueTask DisposeAsync() => throw new NotImplementedException();
    }
}

