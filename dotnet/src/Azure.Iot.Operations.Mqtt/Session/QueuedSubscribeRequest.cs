
using Azure.Iot.Operations.Protocol.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Mqtt.Session
{
    /// <summary>
    /// A single enqueued subscribe and its associated metadata.
    /// </summary>
    internal class QueuedSubscribeRequest : QueuedRequest
    {
        internal MqttClientSubscribeOptions Request { get; }
        
        internal TaskCompletionSource<MqttClientSubscribeResult> ResultTaskCompletionSource { get; }

        internal QueuedSubscribeRequest(
            MqttClientSubscribeOptions request,
            TaskCompletionSource<MqttClientSubscribeResult> resultTaskCompletionSource,
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
