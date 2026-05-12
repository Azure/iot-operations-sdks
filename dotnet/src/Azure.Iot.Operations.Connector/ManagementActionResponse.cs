// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.Telemetry;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// Response to a management action invocation. Returned from the
    /// <see cref="ManagementActionExecutor.OnRequestReceived"/> callback (or, in
    /// connector-worker scenarios, from one of the
    /// <see cref="IManagementActionHandler"/> handler methods); the SDK ships it to
    /// the invoker.
    /// </summary>
    public record ManagementActionResponse
    {
        /// <summary>Serialized response payload.</summary>
        public required ReadOnlySequence<byte> Payload { get; init; }

        /// <summary>MIME type of <see cref="Payload"/> (e.g. <c>application/json</c>).</summary>
        public required string ContentType { get; init; }

        /// <summary>Optional CloudEvent metadata to attach to the response.</summary>
        public required CloudEvent? CloudEvent { get; init; }

        /// <summary>MQTT 5 payload format indicator. Defaults to raw bytes.</summary>
        public MqttPayloadFormatIndicator FormatIndicator { get; init; } = MqttPayloadFormatIndicator.Unspecified;

        /// <summary>Additional MQTT 5 user properties to attach to the response.</summary>
        public Dictionary<string, string>? CustomUserData { get; init; }

        /// <summary>
        /// If set, the response represents an application-level failure. The connector
        /// reports this via the RPC application-error mechanism rather than a successful
        /// response; <see cref="Payload"/> may be empty in that case.
        /// </summary>
        public ManagementActionApplicationError? ApplicationError { get; init; }
    }
}

