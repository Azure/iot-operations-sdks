// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Protocol.Telemetry;

namespace Azure.Iot.Operations.Protocol.State
{
    public abstract class PropertyMaintainer<TProp, TBool> : IAsyncDisposable
        where TProp : class
        where TBool : class
    {
        private bool _isDisposed;

        public PropertyWriteResponder<TProp, TBool> PropertyWriteResponder { get; }

        public PropertyReadResponder<TProp, TBool> PropertyReadResponder { get; }

        public PropertyWatchResponder<TBool> PropertyWatchResponder { get; }

        public PropertyUnwatchResponder<TBool> PropertyUnwatchResponder { get; }

        public PropertyNotifier<TProp> PropertyNotifier { get; }

        public required Func<ExtendedRequest<TProp>, CancellationToken, Task<ExtendedResponse<TBool>>> OnWriteReceived { get; set; }

        public required Func<ExtendedRequest<TBool>, CancellationToken, Task<ExtendedResponse<TProp>>> OnReadReceived { get; set; }

        public required Func<ExtendedRequest<TBool>, CancellationToken, Task<ExtendedResponse<TBool>>> OnWatchReceived { get; set; }

        public required Func<ExtendedRequest<TBool>, CancellationToken, Task<ExtendedResponse<TBool>>> OnUnwatchReceived { get; set; }

        public string? TopicNamespace
        {
            get => PropertyNotifier.TopicNamespace;

            set
            {
                PropertyWriteResponder.TopicNamespace = value;
                PropertyReadResponder.TopicNamespace = value;
                PropertyWatchResponder.TopicNamespace = value;
                PropertyUnwatchResponder.TopicNamespace = value;
                PropertyNotifier.TopicNamespace = value;
            }
        }

        public PropertyMaintainer(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, IPayloadSerializer serializer, string actionTopicToken, Dictionary<string, string>? topicTokenMap = null)
        {
            string topicPattern = AttributeRetriever.GetAttribute<PropertyTopicAttribute>(this)?.Topic ?? string.Empty;
            topicTokenMap ??= new Dictionary<string, string>();

            PropertyWriteResponder = new PropertyWriteResponder<TProp, TBool>(applicationContext, mqttClient, serializer, actionTopicToken, topicTokenMap) { RequestTopicPattern = topicPattern, OnCommandReceived = WriteInt, IsIdempotent = false };
            PropertyReadResponder = new PropertyReadResponder<TProp, TBool>(applicationContext, mqttClient, serializer, actionTopicToken, topicTokenMap) { RequestTopicPattern = topicPattern, OnCommandReceived = ReadInt, IsIdempotent = true };
            PropertyWatchResponder = new PropertyWatchResponder<TBool>(applicationContext, mqttClient, serializer, actionTopicToken, topicTokenMap) { RequestTopicPattern = topicPattern, OnCommandReceived = WatchInt, IsIdempotent = true };
            PropertyUnwatchResponder = new PropertyUnwatchResponder<TBool>(applicationContext, mqttClient, serializer, actionTopicToken, topicTokenMap) { RequestTopicPattern = topicPattern, OnCommandReceived = UnwatchInt, IsIdempotent = true };
            PropertyNotifier = new PropertyNotifier<TProp>(applicationContext, mqttClient, serializer, actionTopicToken, topicTokenMap) { TopicPattern = topicPattern };
        }

        public async Task NotifyAsync(TProp state, OutgoingTelemetryMetadata metadata, Dictionary<string, string>? additionalTopicTokenMap = null, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? notifyTimeout = null, CancellationToken cancellationToken = default)
        {
            await PropertyNotifier.SendTelemetryAsync(state, metadata, additionalTopicTokenMap, qos, notifyTimeout, cancellationToken).ConfigureAwait(false);
        }

        public async Task StartAsync(int? preferredDispatchConcurrency = null, CancellationToken cancellationToken = default)
        {
            await Task.WhenAll(
                PropertyWriteResponder.StartAsync(preferredDispatchConcurrency, cancellationToken),
                PropertyReadResponder.StartAsync(preferredDispatchConcurrency, cancellationToken),
                PropertyWatchResponder.StartAsync(preferredDispatchConcurrency, cancellationToken),
                PropertyUnwatchResponder.StartAsync(preferredDispatchConcurrency, cancellationToken)).ConfigureAwait(false);
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            await Task.WhenAll(
                PropertyWriteResponder.StopAsync(cancellationToken),
                PropertyReadResponder.StopAsync(cancellationToken),
                PropertyWatchResponder.StopAsync(cancellationToken),
                PropertyUnwatchResponder.StopAsync(cancellationToken)).ConfigureAwait(false);
        }

        private Task<ExtendedResponse<TBool>> WriteInt(ExtendedRequest<TProp> request, CancellationToken cancellationToken)
        {
            return OnWriteReceived(request, cancellationToken);
        }

        private Task<ExtendedResponse<TProp>> ReadInt(ExtendedRequest<TBool> request, CancellationToken cancellationToken)
        {
            return OnReadReceived(request, cancellationToken);
        }

        private Task<ExtendedResponse<TBool>> WatchInt(ExtendedRequest<TBool> request, CancellationToken cancellationToken)
        {
            return OnWatchReceived(request, cancellationToken);
        }

        private Task<ExtendedResponse<TBool>> UnwatchInt(ExtendedRequest<TBool> request, CancellationToken cancellationToken)
        {
            return OnUnwatchReceived(request, cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore(false).ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        protected virtual async ValueTask DisposeAsync(bool disposing)
        {
            await DisposeAsyncCore(disposing).ConfigureAwait(false);
        }

        protected virtual async ValueTask DisposeAsyncCore(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    await PropertyWriteResponder.DisposeAsync(disposing).ConfigureAwait(false);
                    await PropertyReadResponder.DisposeAsync(disposing).ConfigureAwait(false);
                    await PropertyWatchResponder.DisposeAsync(disposing).ConfigureAwait(false);
                    await PropertyUnwatchResponder.DisposeAsync(disposing).ConfigureAwait(false);
                    await PropertyNotifier.DisposeAsync(disposing).ConfigureAwait(false);
                }

                _isDisposed = true;
            }
        }
    }
}
