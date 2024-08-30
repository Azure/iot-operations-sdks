
using Azure.Iot.Operations.Protocol.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Mqtt.Session
{
    /// <summary>
    /// A single enqueued publish and its associated metadata.
    /// </summary>
    internal class QueuedPublishRequest : QueuedRequest
    {
        internal MqttApplicationMessage Request { get; }
        
        internal TaskCompletionSource<MqttClientPublishResult> ResultTaskCompletionSource { get; }

        internal QueuedPublishRequest(
            MqttApplicationMessage request,
            TaskCompletionSource<MqttClientPublishResult> resultTaskCompletionSource,
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
