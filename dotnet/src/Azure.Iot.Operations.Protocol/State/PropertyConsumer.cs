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
    public abstract class PropertyConsumer<TProp, TBool> : IAsyncDisposable
        where TProp : class
        where TBool : class
    {
        private bool _isDisposed;

        public PropertyWriteRequester<TProp, TBool> PropertyWriteRequester { get; }

        public PropertyReadRequester<TProp, TBool> PropertyReadRequester { get; }

        public PropertyObserveRequester<TBool> PropertyObserveRequester { get; }

        public PropertyUnobserveRequester<TBool> PropertyUnobserveRequester { get; }

        public PropertyListener<TProp> PropertyListener { get; }

        public required Func<string, TProp, IncomingTelemetryMetadata, Task> OnNotifyReceived { get; set; }

        public string? TopicNamespace
        {
            get => PropertyListener.TopicNamespace;

            set
            {
                PropertyWriteRequester.TopicNamespace = value;
                PropertyReadRequester.TopicNamespace = value;
                PropertyObserveRequester.TopicNamespace = value;
                PropertyUnobserveRequester.TopicNamespace = value;
                PropertyListener.TopicNamespace = value;
            }
        }

        public string? ResponseTopicPrefix
        {
            get => PropertyWriteRequester.ResponseTopicPrefix;

            set
            {
                PropertyWriteRequester.ResponseTopicPrefix = value;
                PropertyReadRequester.ResponseTopicPrefix = value;
                PropertyObserveRequester.ResponseTopicPrefix = value;
                PropertyUnobserveRequester.ResponseTopicPrefix = value;
            }
        }

        public string? ResponseTopicSuffix
        {
            get => PropertyWriteRequester.ResponseTopicSuffix;

            set
            {
                PropertyWriteRequester.ResponseTopicSuffix = value;
                PropertyReadRequester.ResponseTopicSuffix = value;
                PropertyObserveRequester.ResponseTopicSuffix = value;
                PropertyUnobserveRequester.ResponseTopicSuffix = value;
            }
        }

        public string? ResponseTopicPattern
        {
            get => PropertyWriteRequester.ResponseTopicPattern;

            set
            {
                PropertyWriteRequester.ResponseTopicPattern = value;
                PropertyReadRequester.ResponseTopicPattern = value;
                PropertyObserveRequester.ResponseTopicPattern = value;
                PropertyUnobserveRequester.ResponseTopicPattern = value;
            }
        }

        public PropertyConsumer(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, IPayloadSerializer serializer, string actionTopicToken, Dictionary<string, string>? topicTokenMap = null)
        {
            string topicPattern = AttributeRetriever.GetAttribute<PropertyTopicAttribute>(this)?.Topic ?? string.Empty;
            topicTokenMap ??= new Dictionary<string, string>();

            PropertyWriteRequester = new PropertyWriteRequester<TProp, TBool>(applicationContext, mqttClient, serializer, actionTopicToken, topicTokenMap) { RequestTopicPattern = topicPattern };
            PropertyReadRequester = new PropertyReadRequester<TProp, TBool>(applicationContext, mqttClient, serializer, actionTopicToken, topicTokenMap) { RequestTopicPattern = topicPattern };
            PropertyObserveRequester = new PropertyObserveRequester<TBool>(applicationContext, mqttClient, serializer, actionTopicToken, topicTokenMap) { RequestTopicPattern = topicPattern };
            PropertyUnobserveRequester = new PropertyUnobserveRequester<TBool>(applicationContext, mqttClient, serializer, actionTopicToken, topicTokenMap) { RequestTopicPattern = topicPattern };
            PropertyListener = new PropertyListener<TProp>(applicationContext, mqttClient, serializer, actionTopicToken, topicTokenMap) { TopicPattern = topicPattern, OnTelemetryReceived = ReceiveNotificationInt };
        }

        public Task<ExtendedResponse<TBool>> WriteAsync(TProp request, CommandRequestMetadata? metadata = null, Dictionary<string, string>? additionalTopicTokenMap = null, TimeSpan? writeTimeout = default, CancellationToken cancellationToken = default)
        {
            return PropertyWriteRequester.InvokeCommandAsync(request, metadata, additionalTopicTokenMap, writeTimeout, cancellationToken);
        }

        public Task<ExtendedResponse<TProp>> ReadAsync(TBool request, CommandRequestMetadata? metadata = null, Dictionary<string, string>? additionalTopicTokenMap = null, TimeSpan? readTimeout = default, CancellationToken cancellationToken = default)
        {
            return PropertyReadRequester.InvokeCommandAsync(request, metadata, additionalTopicTokenMap, readTimeout, cancellationToken);
        }

        public Task<ExtendedResponse<TBool>> ObserveAsync(TBool request, CommandRequestMetadata? metadata = null, Dictionary<string, string>? additionalTopicTokenMap = null, TimeSpan? observeTimeout = default, CancellationToken cancellationToken = default)
        {
            return PropertyObserveRequester.InvokeCommandAsync(request, metadata, additionalTopicTokenMap, observeTimeout, cancellationToken);
        }

        public Task<ExtendedResponse<TBool>> UnobserveAsync(TBool request, CommandRequestMetadata? metadata = null, Dictionary<string, string>? additionalTopicTokenMap = null, TimeSpan? unobserveTimeout = default, CancellationToken cancellationToken = default)
        {
            return PropertyUnobserveRequester.InvokeCommandAsync(request, metadata, additionalTopicTokenMap, unobserveTimeout, cancellationToken);
        }

        /// <summary>
        /// Begin accepting notifications.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            await PropertyListener.StartAsync(cancellationToken).ConfigureAwait(false);
        }

        private Task ReceiveNotificationInt(string senderId, TProp telemetry, IncomingTelemetryMetadata metadata)
        {
            return OnNotifyReceived(senderId, telemetry, metadata);
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            await PropertyListener.StopAsync(cancellationToken).ConfigureAwait(false);
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
                    await PropertyWriteRequester.DisposeAsync(disposing).ConfigureAwait(false);
                    await PropertyReadRequester.DisposeAsync(disposing).ConfigureAwait(false);
                    await PropertyObserveRequester.DisposeAsync(disposing).ConfigureAwait(false);
                    await PropertyUnobserveRequester.DisposeAsync(disposing).ConfigureAwait(false);
                    await PropertyListener.DisposeAsync(disposing).ConfigureAwait(false);
                }

                _isDisposed = true;
            }
        }
    }
}
