// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Azure.Iot.Operations.Protocol.Telemetry
{
    /// <summary>
    /// The metadata that can be sent with every publish packet sent by a <see cref="TelemetrySender{T}"/>.
    /// </summary>
    public class OutgoingTelemetryMetadata
    {
        /// <summary>
        /// A mandatory timestamp attached to the telemetry message.
        /// </summary>
        /// <remarks>
        /// A message sent by a <see cref="TelemetrySender{T}"/> will include a non-null timestamp. A message sent 
        /// by anything else may or may not include this timestamp.
        /// </remarks>
        public HybridLogicalClock? Timestamp { get; internal set; }

        /// <summary>
        /// A dictionary of user properties that are sent along with the telemetry message from the TelemetrySender.
        /// </summary>
        public Dictionary<string, string> UserData { get; }

        public CloudEvent? CloudEvent { get; set; }

        /// <summary>
        /// If true, this telemetry will be persisted by the AIO MQTT broker upon receiving it. This is only applicable
        /// for retained messages. If this value is set to true, <see cref="Retain"/> must also be set to true.
        /// </summary>
        /// <remarks>
        /// This feature is only applicable with the AIO MQTT broker.
        /// </remarks>
        public bool PersistTelemetry { get; set; }

        /// <summary>
        /// If true, this MQTT message will be retained by the MQTT broker.
        /// </summary>
        public bool Retain { get; set; }

        /// <summary>
        /// Construct an instance with the default values.
        /// </summary>
        /// <remarks>
        /// * The CorrelationData field will be set to a new, random GUID.
        /// * The Timestamp field will be set to the current HybridLogicalClock time for the process.
        /// * The UserData field will be initialized with an empty dictionary; entries in this dictionary can be set by user code as desired.
        /// </remarks>
        public OutgoingTelemetryMetadata()
        {
            UserData = [];
            Timestamp = null;
        }
    }
}
