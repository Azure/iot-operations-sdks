// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;

#pragma warning disable IDE0060 // Remove unused parameter
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
#pragma warning disable CS0168 // Variable is declared but never used

namespace Azure.Iot.Operations.Protocol.Streaming
{
    public abstract class StreamingCommandInvoker<TReq, TResp> : IAsyncDisposable
        where TReq : class
        where TResp : class
    {
        private static readonly TimeSpan DefaultExchangeTimeout = TimeSpan.FromSeconds(10);

        private readonly ApplicationContext _applicationContext;
        private readonly IMqttPubSubClient _mqttClient;
        private readonly string _commandName;
        private readonly IPayloadSerializer _serializer;

        // POC: one in-flight exchange at a time, so these per-exchange values live on the instance.
        private string _requestTopic = string.Empty;
        private string _responseTopic = string.Empty;
        private uint _exchangeTimeoutSeconds;

        // The MQTT callback writes received response entries here; the caller reads them as an async stream.
        private Channel<StreamingExtendedResponse<TResp>>? _responseChannel;
        private Guid _correlationId;
        private HashSet<uint> _seenResponseIndexes = new();
        private ExchangeContext? _exchangeContext;

        /// <summary>
        /// The topic token replacement map that this streaming command invoker will use by default. Generally, this will include the token values
        /// for topic tokens such as "modelId" which should be the same for the duration of this command invoker's lifetime.
        /// </summary>
        /// <remarks>
        /// Tokens replacement values can also be specified per-method invocation by specifying the additionalTopicToken map in <see cref="InvokeStreamingCommandAsync"/>.
        /// </remarks>
        public Dictionary<string, string> TopicTokenMap { get; protected set; }

        public string RequestTopicPattern { get; init; }

        public string? TopicNamespace { get; set; }

        /// <summary>
        /// The prefix to use in the command response topic. This value is ignored if <see cref="ResponseTopicPattern"/> is set.
        /// </summary>
        /// <remarks>
        /// If no prefix or suffix is specified, and no value is provided in <see cref="ResponseTopicPattern"/>, then this
        /// value will default to "clients/{invokerClientId}" for security purposes.
        /// 
        /// If a prefix and/or suffix are provided, then the response topic will use the format:
        /// {prefix}/{command request topic}/{suffix}.
        /// </remarks>
        public string? ResponseTopicPrefix { get; set; }

        /// <summary>
        /// The suffix to use in the command response topic. This value is ignored if <see cref="ResponseTopicPattern"/> is set.
        /// </summary>
        /// <remarks>
        /// If no suffix is specified, then the command response topic won't include a suffix.
        /// 
        /// If a prefix and/or suffix are provided, then the response topic will use the format:
        /// {prefix}/{command request topic}/{suffix}.
        /// </remarks>
        public string? ResponseTopicSuffix { get; set; }

        /// <summary>
        /// If provided, this topic pattern will be used for command response topic.
        /// </summary>
        /// <remarks>
        /// If not provided, and no value is provided for <see cref="ResponseTopicPrefix"/> or <see cref="ResponseTopicSuffix"/>, the default pattern used will be clients/{mqtt client id}/{request topic pattern}.
        /// </remarks>
        public string? ResponseTopicPattern { get; set; }

        // The invoker always auto-acknowledges responses; it cannot resume a response stream after a crash, so manual ack is executor-only.

        /// <summary>POC diagnostics hook: receives protocol-level trace lines (topics, publishes, receipts).</summary>
        public Action<string>? Log { get; set; }

        public StreamingCommandInvoker(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, string commandName, IPayloadSerializer serializer)
        {
            _applicationContext = applicationContext;
            _mqttClient = mqttClient ?? throw new ArgumentNullException(nameof(mqttClient));
            _commandName = commandName ?? throw new ArgumentNullException(nameof(commandName));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));

            RequestTopicPattern = AttributeRetriever.GetAttribute<CommandTopicAttribute>(this)?.RequestTopic ?? string.Empty;
            TopicTokenMap = new();

            // Responses arrive on the invoker's unique response topic; the receive step consumes them.
            _mqttClient.ApplicationMessageReceivedAsync += MessageReceivedCallbackAsync;
        }

        /// <summary>
        /// Invoke a streaming command on a particular streaming command executor
        /// </summary>
        /// <param name="requests">The stream of requests to send. This stream must contain at least one request.</param>
        /// <param name="streamMetadata">The metadata for the request stream as a whole.</param>
        /// <param name="additionalTopicTokenMap">Topic tokens to substitute in the request topic.</param>
        /// <param name="exchangeTimeout">
        /// The total time budget for the whole exchange, counted from its start and never reset. It is the backstop for an exchange that
        /// never gracefully completes (a stream that never closes, a lost final message, or a crashed peer). A configurable default applies when null.
        /// </param>
        /// <param name="cancellationToken">
        /// Cancellation token. Signalling this will also make a single attempt to notify the executor of the cancellation. To make multiple attempts to cancel and/or
        /// check that this cancellation succeeded, use <see cref="IExchangeContext.CancelAsync(Dictionary{string, string}?, CancellationToken)"/> instead.
        /// </param>
        /// <returns>The response stream (await <see cref="IResponseStreamContext{T}.StreamMetadata"/> for the response stream metadata) together with the exchange context for lifecycle and control.</returns>
        public async Task<(IResponseStreamContext<StreamingExtendedResponse<TResp>> Responses, IExchangeContext Exchange)> InvokeStreamingCommandAsync(
            IAsyncEnumerable<StreamingExtendedRequest<TReq>> requests,
            RequestStreamMetadata? streamMetadata = null,
            Dictionary<string, string>? additionalTopicTokenMap = null,
            TimeSpan? exchangeTimeout = null,
            CancellationToken cancellationToken = default)
        {
            // Combine the invoker's default topic tokens with any provided for this invocation.
            Dictionary<string, string> combinedTopicTokenMap = new(TopicTokenMap);
            if (additionalTopicTokenMap != null)
            {
                foreach (KeyValuePair<string, string> token in additionalTopicTokenMap)
                {
                    combinedTopicTokenMap[token.Key] = token.Value;
                }
            }

            // Resolve this exchange's request/response topics and total time budget once (helpers ported from CommandInvoker).
            _requestTopic = GetCommandTopic(RequestTopicPattern, combinedTopicTokenMap);
            _responseTopic = GetCommandTopic(GenerateResponseTopicPattern(combinedTopicTokenMap), combinedTopicTokenMap);
            _exchangeTimeoutSeconds = (uint)(exchangeTimeout ?? DefaultExchangeTimeout).TotalSeconds;

            // One correlation id identifies the whole exchange (both request and response streams carry it).
            Guid correlationId = streamMetadata?.CorrelationId ?? Guid.NewGuid();
            _correlationId = correlationId;

            Log?.Invoke($"invoker: exchange {correlationId} cmd '{_commandName}' req='{_requestTopic}' resp='{_responseTopic}' timeout={_exchangeTimeoutSeconds}s");

            // The response channel is the push -> pull bridge: the MQTT callback writes entries, the caller reads them.
            _responseChannel = Channel.CreateUnbounded<StreamingExtendedResponse<TResp>>();
            _seenResponseIndexes = new();
            IAsyncEnumerable<StreamingExtendedResponse<TResp>> responses = _responseChannel.Reader.ReadAllAsync(cancellationToken);
            ExchangeContext exchangeContext = new();
            _exchangeContext = exchangeContext;

            // Subscribe to the response topic BEFORE sending, so early responses are not missed.
            await _mqttClient.SubscribeAsync(new MqttClientSubscribeOptions(_responseTopic, MqttQualityOfServiceLevel.AtLeastOnce), cancellationToken);
            Log?.Invoke($"invoker: subscribed to response topic '{_responseTopic}'");

            // Pump the request stream concurrently so the caller can read responses while requests are still going out.
            // This lets a request source react to responses (e.g. wait for each pong before sending the next ping).
            _ = Task.Run(() => PumpRequestsAsync(correlationId, requests, streamMetadata, cancellationToken), cancellationToken);

            // Hand back the response stream (drained via the channel) and the exchange handle.
            ResponseStreamContext<StreamingExtendedResponse<TResp>> responseContext =
                new(responses, Task.FromResult(new ResponseStreamMetadata()));
            return (responseContext, exchangeContext);
        }

        /// <summary>
        /// Invoke a streaming command for the common request-response-streaming shape: a single plain request in,
        /// a directly awaitable-foreach stream of plain response payloads out. This is a thin convenience wrapper
        /// over <see cref="InvokeStreamingCommandAsync"/> for callers who don't need a request stream, per-entry
        /// metadata, or the exchange context - e.g.:
        /// <code>
        /// await foreach (var item in invoker.ExecuteStreamingAsync(request))
        /// {
        ///     await ProcessAsync(item);
        /// }
        /// </code>
        /// Callers that need to stream multiple requests, inspect response metadata, or drive cancellation should
        /// use <see cref="InvokeStreamingCommandAsync"/> directly instead.
        /// </summary>
        /// <param name="request">The single request payload to send.</param>
        /// <param name="streamMetadata">The metadata for the request stream as a whole.</param>
        /// <param name="additionalTopicTokenMap">Topic tokens to substitute in the request topic.</param>
        /// <param name="exchangeTimeout">The total time budget for the whole exchange. A configurable default applies when null.</param>
        /// <param name="cancellationToken">Cancellation token for both sending the request and reading the response stream.</param>
        /// <returns>The response payloads, in the order the executor emitted them.</returns>
        public async IAsyncEnumerable<TResp> ExecuteStreamingAsync(
            TReq request,
            RequestStreamMetadata? streamMetadata = null,
            Dictionary<string, string>? additionalTopicTokenMap = null,
            TimeSpan? exchangeTimeout = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            (IResponseStreamContext<StreamingExtendedResponse<TResp>> responses, IExchangeContext _) = await InvokeStreamingCommandAsync(
                SingleRequestEntryStream(request),
                streamMetadata,
                additionalTopicTokenMap,
                exchangeTimeout,
                cancellationToken);

            await foreach (StreamingExtendedResponse<TResp> response in responses.Entries.WithCancellation(cancellationToken))
            {
                yield return response.Payload;
            }
        }

        // Wraps a single plain request as the one-entry request stream that InvokeStreamingCommandAsync requires.
        private static async IAsyncEnumerable<StreamingExtendedRequest<TReq>> SingleRequestEntryStream(TReq request)
        {
            await Task.Yield();
            yield return new StreamingExtendedRequest<TReq>(request);
        }

        // Publishes the request stream: each entry as a `d:<index>` message, then a closing `last`.
        private async Task PumpRequestsAsync(
            Guid correlationId,
            IAsyncEnumerable<StreamingExtendedRequest<TReq>> requests,
            RequestStreamMetadata? streamMetadata,
            CancellationToken cancellationToken)
        {
            // The invoker assigns each entry its index, in send order.
            uint index = 0;
            await foreach (StreamingExtendedRequest<TReq> request in requests.WithCancellation(cancellationToken))
            {
                await PublishRequestEntryAsync(correlationId, index, request, streamMetadata, cancellationToken);
                index++;
            }

            // A standalone `last` control message closes the request stream at the next index.
            await PublishLastAsync(correlationId, index, cancellationToken);
        }

        // Builds and publishes one request-stream entry as a QoS 1 `d:<index>` message.
        private async Task PublishRequestEntryAsync(
            Guid correlationId,
            uint index,
            StreamingExtendedRequest<TReq> request,
            RequestStreamMetadata? streamMetadata,
            CancellationToken cancellationToken)
        {
            string clientId = _mqttClient.ClientId
                ?? throw new InvalidOperationException("Must be connected to the MQTT broker before invoking a streaming command.");

            MqttApplicationMessage message = new(_requestTopic, MqttQualityOfServiceLevel.AtLeastOnce)
            {
                ResponseTopic = _responseTopic,
                CorrelationData = correlationId.ToByteArray(),
                MessageExpiryInterval = request.MessageExpiry is TimeSpan e ? (uint)e.TotalSeconds : _exchangeTimeoutSeconds,
            };

            // `$partition` pins every packet of this exchange to the same executor in the shared subscription.
            message.AddUserProperty("$partition", clientId);
            message.AddUserProperty(AkriSystemProperties.SourceId, clientId);

            // The HLC timestamp travels with every entry; it also feeds de-dup and executor-restart detection.
            string timestamp = await _applicationContext.ApplicationHlc.UpdateNowAsync(cancellationToken: cancellationToken);
            message.AddUserProperty(AkriSystemProperties.Timestamp, timestamp);

            // The streaming tag: this is data entry `index`, carrying the remaining exchange budget.
            message.AddUserProperty(StreamProperty.Name, StreamProperty.Data(index, _exchangeTimeoutSeconds));
            Log?.Invoke($"invoker -> data idx={index} \"{request.Payload}\"");

            SerializedPayloadContext payload = _serializer.ToBytes(request.Payload);
            if (!payload.SerializedPayload.IsEmpty)
            {
                message.Payload = payload.SerializedPayload;
                message.PayloadFormatIndicator = (MqttPayloadFormatIndicator)payload.PayloadFormatIndicator;
                message.ContentType = payload.ContentType;
            }

            await _mqttClient.PublishAsync(message, cancellationToken);
        }

        // Closes the request stream with a standalone `last` control message (no payload).
        private async Task PublishLastAsync(Guid correlationId, uint index, CancellationToken cancellationToken)
        {
            string clientId = _mqttClient.ClientId
                ?? throw new InvalidOperationException("Must be connected to the MQTT broker before invoking a streaming command.");

            MqttApplicationMessage message = new(_requestTopic, MqttQualityOfServiceLevel.AtLeastOnce)
            {
                ResponseTopic = _responseTopic,
                CorrelationData = correlationId.ToByteArray(),
                MessageExpiryInterval = _exchangeTimeoutSeconds,
            };

            message.AddUserProperty("$partition", clientId);
            message.AddUserProperty(AkriSystemProperties.SourceId, clientId);
            message.AddUserProperty(StreamProperty.Name, StreamProperty.Last(index, _exchangeTimeoutSeconds));
            Log?.Invoke($"invoker -> last idx={index}");

            await _mqttClient.PublishAsync(message, cancellationToken);
        }

        // Handles response-topic messages: filters by correlation, classifies via __stream, and feeds the response channel.
        private Task MessageReceivedCallbackAsync(MqttApplicationMessageReceivedEventArgs args)
        {
            MqttApplicationMessage message = args.ApplicationMessage;

            // Ignore anything that isn't a response for this exchange.
            if (message.CorrelationData == null
                || !GuidExtensions.TryParseBytes(message.CorrelationData, out Guid? correlationId)
                || correlationId != _correlationId
                || _responseChannel == null)
            {
                return Task.CompletedTask;
            }

            args.AutoAcknowledge = true;

            string? streamValue = message.UserProperties?.FirstOrDefault(p => p.Name == StreamProperty.Name)?.Value;
            if (!StreamProperty.TryParse(streamValue, out StreamTag tag))
            {
                return Task.CompletedTask;
            }

            switch (tag.Kind)
            {
                case StreamMessageKind.Data:
                    // De-dup redeliveries by index (POC; timestamp would be added for restart detection).
                    if (_seenResponseIndexes.Add(tag.Index))
                    {
                        TResp payload = _serializer.FromBytes<TResp>(message.Payload, message.ContentType, message.PayloadFormatIndicator);
                        Log?.Invoke($"invoker <- data idx={tag.Index} \"{payload}\"");
                        StreamMessageMetadata metadata = new() { Index = tag.Index };
                        _responseChannel.Writer.TryWrite(new StreamingExtendedResponse<TResp>(payload, metadata));
                    }
                    else
                    {
                        Log?.Invoke($"invoker <- data idx={tag.Index} (duplicate, ignored)");
                    }
                    break;

                case StreamMessageKind.Control when tag.Control == StreamControlCommand.Last:
                    // A `last` control message closes the response stream; completing the channel ends the caller's await foreach.
                    Log?.Invoke($"invoker <- last idx={tag.Index} (response stream complete)");
                    _responseChannel.Writer.TryComplete();
                    _exchangeContext?.Complete();
                    break;
            }

            return Task.CompletedTask;
        }

        // Resolves a topic pattern (request or response) against the token map for this exchange.
        private static string GetCommandTopic(string pattern, Dictionary<string, string> topicTokenMap) =>
            MqttTopicProcessor.ResolveTopic(pattern, topicTokenMap);

        // Builds the invoker's unique response topic pattern (defaults to clients/{clientId}/{requestPattern}).
        private string GenerateResponseTopicPattern(Dictionary<string, string> topicTokenMap)
        {
            if (ResponseTopicPattern != null)
            {
                return ResponseTopicPattern;
            }

            string prefix = ResponseTopicPrefix ?? $"clients/{_mqttClient.ClientId}";
            string pattern = $"{prefix}/{RequestTopicPattern}";
            return ResponseTopicSuffix != null ? $"{pattern}/{ResponseTopicSuffix}" : pattern;
        }

        public ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            return ValueTask.CompletedTask;
        }
#pragma warning restore IDE0060 // Remove unused parameter
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
#pragma warning restore CS0168 // Variable is declared but never used

    }
}
