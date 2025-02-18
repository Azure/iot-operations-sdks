﻿// Copyright (c) Microsoft Corporation.
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
        public HybridLogicalClock Timestamp { get; set; } // TODO Needed a set to set the HLC from Telemetry Sender.

        /// <summary>
        /// A dictionary of user properties that are sent along with the telemetry message from the TelemetrySender.
        /// </summary>
        public Dictionary<string, string> UserData { get; }



        public CloudEvent? CloudEvent { get; set; }

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
            //HybridLogicalClock localClock = HybridLogicalClock.GetInstance();
            //localClock.Update();
            //Timestamp = new HybridLogicalClock();
            UserData = [];
            Timestamp = null;
        }
    }
}
