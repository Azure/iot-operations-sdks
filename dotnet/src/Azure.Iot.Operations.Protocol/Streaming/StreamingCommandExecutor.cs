// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;
#pragma warning disable IDE0060 // Remove unused parameter
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace Azure.Iot.Operations.Protocol.Streaming
{
    public abstract class StreamingCommandExecutor<TReq, TResp> : IAsyncDisposable
        where TReq : class
        where TResp : class
    {
        private readonly ApplicationContext _applicationContext;
        private readonly IMqttPubSubClient _mqttClient;
        private readonly string _commandName;
        private readonly IPayloadSerializer _serializer;

        // The shared-subscription group; when set, requests are load-balanced across executors.
        public string ServiceGroupId { get; init; }
        private string _subscriptionTopic = string.Empty;

        // POC: one in-flight exchange at a time.
        private Channel<ReceivedStreamingExtendedRequest<TReq>>? _requestChannel;
        private Guid _correlationId;
        private string _responseTopic = string.Empty;
        private HashSet<uint> _seenRequestIndexes = new();
        private ExchangeContext? _exchangeContext;

        /// <summary>
        /// A streaming command was invoked
        /// </summary>
        /// <remarks>
        /// The callback provides the request stream, its stream-level metadata, and the exchange context, and requires the user to return one to many responses together with the response stream's metadata.
        /// </remarks>
        public required Func<IStreamContext<ReceivedStreamingExtendedRequest<TReq>>, RequestStreamMetadata, IExchangeContext, (IAsyncEnumerable<StreamingExtendedResponse<TResp>> Responses, ResponseStreamMetadata Metadata)> OnStreamingCommandReceived { get; set; }

        public string RequestTopicPattern { get; init; }

        /// <summary>
        /// The topic token replacement map that this executor will use by default. Generally, this will include the token values
        /// for topic tokens such as "executorId" which should be the same for the duration of this command executor's lifetime.
        /// </summary>
        /// <remarks>
        /// Tokens replacement values can also be specified when starting the executor by specifying the additionalTopicToken map in <see cref="StartAsync(CancellationToken)"/>.
        /// </remarks>
        public Dictionary<string, string> TopicTokenMap { get; protected set; }

        /// <summary>
        /// If true, this executor will acknowledge the MQTT message associated with each streaming request as soon as it arrives.
        /// If false, the user must call <see cref="ReceivedStreamingExtendedRequest{TReq}.AcknowledgeAsync"/> once they are done processing
        /// each request message.
        /// </summary>
        /// <remarks>
        /// Generally, delaying acknowledgement allows for re-delivery by the broker in cases where the executor crashes or restarts unexpectedly.
        /// However, MQTT acknowledgements must be delivered in order, so delaying these acknowledgements may affect the flow of acknowledgements
        /// being sent by other processes using this same MQTT client. Additionally, the MQTT broker has a limit on the number of un-acknowledged messages
        /// that are allowed to be in-flight at a single moment, so delaying too many acknowledgements may halt all further MQTT traffic on the underlying
        /// MQTT client.
        /// </remarks>
        public bool AutomaticallyAcknowledgeRequests { get; set; } = true;

        /// <summary>POC diagnostics hook: receives protocol-level trace lines (subscription, receipts, publishes).</summary>
        public Action<string>? Log { get; set; }

        public StreamingCommandExecutor(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, string commandName, IPayloadSerializer serializer)
        {
            _applicationContext = applicationContext;
            _mqttClient = mqttClient ?? throw new ArgumentNullException(nameof(mqttClient));
            _commandName = commandName ?? throw new ArgumentNullException(nameof(commandName));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));

            ServiceGroupId = AttributeRetriever.GetAttribute<ServiceGroupIdAttribute>(this)?.Id ?? string.Empty;
            RequestTopicPattern = AttributeRetriever.GetAttribute<CommandTopicAttribute>(this)?.RequestTopic ?? string.Empty;
            TopicTokenMap = new();

            // Requests arrive on the command topic; the receive step consumes them.
            _mqttClient.ApplicationMessageReceivedAsync += MessageReceivedCallbackAsync;
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            // A shared subscription load-balances requests across executors in the same group.
            string requestTopic = GetCommandTopic(RequestTopicPattern, TopicTokenMap);
            _subscriptionTopic = ServiceGroupId != string.Empty ? $"$share/{ServiceGroupId}/{requestTopic}" : requestTopic;
            Log?.Invoke($"executor: subscribing to '{_subscriptionTopic}'");

            MqttClientSubscribeOptions subscribeOptions = new(new MqttTopicFilter(_subscriptionTopic, MqttQualityOfServiceLevel.AtLeastOnce));
            await _mqttClient.SubscribeAsync(subscribeOptions, cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrEmpty(_subscriptionTopic))
            {
                await _mqttClient.UnsubscribeAsync(new MqttClientUnsubscribeOptions(_subscriptionTopic), cancellationToken);
                _subscriptionTopic = string.Empty;
            }
        }

        // Handles command-topic messages: sets up the exchange on the first request, then feeds each request entry to the handler.
        private Task MessageReceivedCallbackAsync(MqttApplicationMessageReceivedEventArgs args)
        {
            MqttApplicationMessage message = args.ApplicationMessage;

            if (!MqttTopicProcessor.DoesTopicMatchFilter(message.Topic, GetCommandTopic(RequestTopicPattern, TopicTokenMap))
                || message.CorrelationData == null
                || !GuidExtensions.TryParseBytes(message.CorrelationData, out Guid? correlationId))
            {
                return Task.CompletedTask;
            }

            string? streamValue = message.UserProperties?.FirstOrDefault(p => p.Name == StreamProperty.Name)?.Value;
            if (!StreamProperty.TryParse(streamValue, out StreamTag tag))
            {
                return Task.CompletedTask;
            }

            args.AutoAcknowledge = AutomaticallyAcknowledgeRequests;

            // First message of a new exchange: create the request stream + context and start the user's handler.
            if (_requestChannel == null || correlationId != _correlationId)
            {
                StartExchange(correlationId!.Value, message.ResponseTopic);
            }

            switch (tag.Kind)
            {
                case StreamMessageKind.Data:
                    // De-dup redeliveries by index (POC; timestamp added later for restart detection).
                    if (_seenRequestIndexes.Add(tag.Index))
                    {
                        TReq payload = _serializer.FromBytes<TReq>(message.Payload, message.ContentType, message.PayloadFormatIndicator);
                        Log?.Invoke($"executor <- data idx={tag.Index} \"{payload}\"");
                        StreamMessageMetadata metadata = new() { Index = tag.Index };
                        _requestChannel!.Writer.TryWrite(new ReceivedStreamingExtendedRequest<TReq>(payload, metadata, Task.CompletedTask));
                    }
                    else
                    {
                        Log?.Invoke($"executor <- data idx={tag.Index} (duplicate, ignored)");
                    }
                    break;

                case StreamMessageKind.Control when tag.Control == StreamControlCommand.Last:
                    // Request stream closed; completing the channel ends the handler's await foreach.
                    Log?.Invoke($"executor <- last idx={tag.Index} (request stream complete)");
                    _requestChannel!.Writer.TryComplete();
                    break;
            }

            return Task.CompletedTask;
        }

        // Sets up a new exchange and runs the user's handler, pumping its responses back to the invoker.
        private void StartExchange(Guid correlationId, string? responseTopic)
        {
            _correlationId = correlationId;
            _responseTopic = responseTopic ?? string.Empty;
            _seenRequestIndexes = new();
            _requestChannel = Channel.CreateUnbounded<ReceivedStreamingExtendedRequest<TReq>>();
            _exchangeContext = new ExchangeContext();
            Log?.Invoke($"executor: new exchange {correlationId}, responseTopic='{_responseTopic}'");

            StreamContext<ReceivedStreamingExtendedRequest<TReq>> requestStream = new(_requestChannel.Reader.ReadAllAsync());
            RequestStreamMetadata requestMetadata = new() { CorrelationId = correlationId };

            (IAsyncEnumerable<StreamingExtendedResponse<TResp>> responses, ResponseStreamMetadata _) =
                OnStreamingCommandReceived(requestStream, requestMetadata, _exchangeContext);

            // POC: fire-and-forget pump; a real impl would track/await it and surface failures.
            _ = PumpResponsesAsync(correlationId, responses);
        }

        // Drains the handler's responses, publishing each as `d:index`, then closes with `last`.
        private async Task PumpResponsesAsync(Guid correlationId, IAsyncEnumerable<StreamingExtendedResponse<TResp>> responses)
        {
            uint index = 0;
            await foreach (StreamingExtendedResponse<TResp> response in responses)
            {
                await PublishResponseEntryAsync(correlationId, index, response, CancellationToken.None);
                index++;
            }

            await PublishResponseLastAsync(correlationId, index, CancellationToken.None);
            _exchangeContext?.Complete();
        }

        // Publishes one response-stream entry as a QoS 1 `d:<index>` message on the invoker's response topic.
        private async Task PublishResponseEntryAsync(Guid correlationId, uint index, StreamingExtendedResponse<TResp> response, CancellationToken cancellationToken)
        {
            MqttApplicationMessage message = new(_responseTopic, MqttQualityOfServiceLevel.AtLeastOnce)
            {
                CorrelationData = correlationId.ToByteArray(),
            };

            // The HLC timestamp travels with every entry; it also feeds de-dup and executor-restart detection.
            string timestamp = await _applicationContext.ApplicationHlc.UpdateNowAsync(cancellationToken: cancellationToken);
            message.AddUserProperty(AkriSystemProperties.Timestamp, timestamp);

            // Response direction: no timeout in the tag and no $partition (the response topic is unique to the invoker).
            message.AddUserProperty(StreamProperty.Name, StreamProperty.Data(index));
            Log?.Invoke($"executor -> data idx={index} \"{response.Payload}\"");

            SerializedPayloadContext payload = _serializer.ToBytes(response.Payload);
            if (!payload.SerializedPayload.IsEmpty)
            {
                message.Payload = payload.SerializedPayload;
                message.PayloadFormatIndicator = (MqttPayloadFormatIndicator)payload.PayloadFormatIndicator;
                message.ContentType = payload.ContentType;
            }

            await _mqttClient.PublishAsync(message, cancellationToken);
        }

        // Closes the response stream with a standalone `last` control message (no payload).
        private async Task PublishResponseLastAsync(Guid correlationId, uint index, CancellationToken cancellationToken)
        {
            MqttApplicationMessage message = new(_responseTopic, MqttQualityOfServiceLevel.AtLeastOnce)
            {
                CorrelationData = correlationId.ToByteArray(),
            };

            message.AddUserProperty(StreamProperty.Name, StreamProperty.Last(index));
            Log?.Invoke($"executor -> last idx={index}");

            await _mqttClient.PublishAsync(message, cancellationToken);
        }

        // Resolves the executor's request topic against the token map.
        private static string GetCommandTopic(string pattern, Dictionary<string, string> topicTokenMap) =>
            MqttTopicProcessor.ResolveTopic(pattern, topicTokenMap);

        public async ValueTask DisposeAsync()
        {
            await StopAsync();

            GC.SuppressFinalize(this);
        }
    }
}
#pragma warning restore IDE0060 // Remove unused parameter
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
