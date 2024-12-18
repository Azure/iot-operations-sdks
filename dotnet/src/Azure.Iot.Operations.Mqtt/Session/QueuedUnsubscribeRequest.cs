// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Mqtt.Session
{
    /// <summary>
    /// A single enqueued unsubscribe and its associated metadata.
    /// </summary>
    internal class QueuedUnsubscribeRequest : QueuedRequest
    {
        internal MqttClientUnsubscribeOptions Request { get; }
        
        internal TaskCompletionSource<MqttClientUnsubscribeResult> ResultTaskCompletionSource { get; }

        internal QueuedUnsubscribeRequest(
            MqttClientUnsubscribeOptions request,
            TaskCompletionSource<MqttClientUnsubscribeResult> resultTaskCompletionSource,
            CancellationToken cancellationToken = default)
            : base(cancellationToken)
        {
            Request = request;
            ResultTaskCompletionSource = resultTaskCompletionSource;
        }

        internal override void OnException(Exception reason)
        {
            ResultTaskCompletionSource.TrySetException(reason);
        }
    }
}
