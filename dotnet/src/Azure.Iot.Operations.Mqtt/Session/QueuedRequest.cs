// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;

namespace Azure.Iot.Operations.Mqtt.Session
{
    /// <summary>
    /// A single enqueued publish, subscribe, or unsubscribe request and its associated metadata.
    /// </summary>
    internal abstract class QueuedRequest
    {
        /// <summary>
        /// The cancellation token for this particular request.
        /// </summary>
        internal CancellationToken CancellationToken { get; set; }

        /// <summary>
        /// If the request has been sent to the MQTT broker. This value will be reset if the connection is lost prior to this request
        /// being acknowledged.
        /// </summary>
        internal bool IsInFlight { get; set; }

        internal QueuedRequest(CancellationToken cancellationToken = default)
        {
            CancellationToken = cancellationToken;
        }

        abstract internal void OnException(Exception reason);
    }
}
