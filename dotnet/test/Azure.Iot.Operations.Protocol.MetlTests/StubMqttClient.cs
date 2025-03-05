// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using Microsoft.VisualStudio.Threading;
using MQTTnet;
using MQTTnet.Diagnostics.PacketInspection;
using MQTTnet.Exceptions;
using MQTTnet.Formatter;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using Xunit;

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    internal class StubMqttClient : MQTTnet.IMqttClient
    {
        private static readonly TimeSpan TestTimeout = TimeSpan.FromMinutes(1);

        private static readonly MqttConnAckPacket SuccessfulReconnectConnAck = new()
        {
            ReasonCode = MqttConnectReasonCode.Success,
            IsSessionPresent = true,
        };

        private readonly AsyncAtomicInt _packetIdSequencer;
        private readonly AsyncQueue<TestAckKind> _pubAckQueue;
        private readonly AsyncQueue<TestAckKind> _subAckQueue;
        private readonly AsyncQueue<TestAckKind> _unsubAckQueue;
        private readonly AsyncQueue<ushort> _ackedPacketIds;
        private readonly AsyncAtomicInt _publicationCount;
        private readonly AsyncAtomicInt _acknowledgementCount;
        private readonly AsyncQueue<byte[]> _publishedCorrelationIds;
        private readonly ConcurrentDictionary<string, bool> _subscribedTopics;
        private readonly ConcurrentDictionary<Guid, MqttApplicationMessage> _publishedMessages;
        private readonly ConcurrentDictionary<int, MqttApplicationMessage> _publishedMessageSeq;

        public StubMqttClient(string clientId)
        {
            ApplicationMessageReceivedAsync = _ => Task.CompletedTask;
            ConnectedAsync = _ => Task.CompletedTask;
            ConnectingAsync = _ => Task.CompletedTask;
            DisconnectedAsync = _ => Task.CompletedTask;
            InspectPacketAsync = _ => Task.CompletedTask;
            IsConnected = false;

            Options = new MqttClientOptions() { ClientId = clientId, ProtocolVersion = MqttProtocolVersion.V500 };

            _packetIdSequencer = new(0);
            _pubAckQueue = new();
            _subAckQueue = new();
            _unsubAckQueue = new();
            _ackedPacketIds = new();
            _publicationCount = new(0);
            _acknowledgementCount = new(0);
            _publishedCorrelationIds = new();
            _subscribedTopics = new();
            _publishedMessages = new();
            _publishedMessageSeq = new();
        }

        event Func<InspectMqttPacketEventArgs, Task> MQTTnet.IMqttClient.InspectPacketAsync
        {
            add
            {
                throw new NotImplementedException();
            }

            remove
            {
                throw new NotImplementedException();
            }
        }

        public event Func<MqttApplicationMessageReceivedEventArgs, Task>? ApplicationMessageReceivedAsync;

        public event Func<MqttClientConnectedEventArgs, Task> ConnectedAsync;

        public event Func<MqttClientConnectingEventArgs, Task> ConnectingAsync;

        public event Func<MqttClientDisconnectedEventArgs, Task> DisconnectedAsync;

        public event Func<InspectMqttPacketEventArgs, Task> InspectPacketAsync;

        public bool IsConnected { get; private set; }

        public MqttClientOptions Options { get; private set; }

        internal async Task<int> GetPublicationCount()
        {
            return await _publicationCount.Read().ConfigureAwait(false);
        }

        internal async Task<int> GetAcknowledgementCount()
        {
            return await _acknowledgementCount.Read().ConfigureAwait(false);
        }

        public string ClientId => Options.ClientId;

        public MqttProtocolVersion ProtocolVersion => Options.ProtocolVersion;

        public async Task<MqttClientConnectResult> ConnectAsync(MqttClientOptions options, CancellationToken cancellationToken = default)
        {
            IsConnected = true;
            var connectResult = new MqttClientConnectResultFactory().Create(SuccessfulReconnectConnAck, MqttProtocolVersion.V500);
            Options = options;
            await ConnectedAsync.Invoke(new MqttClientConnectedEventArgs(connectResult)).WaitAsync(TestTimeout);
            return connectResult;
        }

        public Task DisconnectAsync(MqttClientDisconnectOptions options, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool wasConnected = IsConnected;
            IsConnected = false;
            return DisconnectedAsync.Invoke(new MqttClientDisconnectedEventArgs(wasConnected, new MqttClientConnectResult(), MqttClientDisconnectReason.NormalDisconnection, "disconnected", new List<MqttUserProperty>(), null));
        }

        public Task PingAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public async Task<MqttClientPublishResult> PublishAsync(MqttApplicationMessage applicationMessage, CancellationToken cancellationToken = default)
        {
            int sequenceIndex = await _publicationCount.Increment().ConfigureAwait(false) - 1;

            _publishedMessageSeq.TryAdd(sequenceIndex, applicationMessage);

            if (!GuidExtensions.TryParseBytes(applicationMessage.CorrelationData ?? Array.Empty<byte>(), out Guid? correlationId))
            {
                correlationId = Guid.Empty;
            }

            _publishedMessages.TryAdd((Guid)correlationId!, applicationMessage);
            _publishedCorrelationIds.Enqueue(applicationMessage.CorrelationData ?? Array.Empty<byte>());

            if (!_pubAckQueue.TryDequeue(out TestAckKind ackKind) || ackKind == TestAckKind.Success)
            {
                return new MqttClientPublishResult(0, MqttClientPublishReasonCode.Success, string.Empty, new List<MqttUserProperty>());
            }
            else if (ackKind == TestAckKind.Fail)
            {
                return new MqttClientPublishResult(0, MqttClientPublishReasonCode.UnspecifiedError, string.Empty, null);
            }
            else if (ackKind == TestAckKind.Drop)
            {
                throw new MqttCommunicationException("Timed out waiting for a PubAck");
            }
            else
            {
                Assert.Fail($"unrecognized {nameof(TestAckKind)}: {ackKind}");
                return null!;
            }
        }

        public Task<MqttClientSubscribeResult> SubscribeAsync(MqttClientSubscribeOptions options, CancellationToken cancellationToken = default)
        {
            foreach (MqttTopicFilter topicFilter in options.TopicFilters)
            {
                _subscribedTopics[topicFilter.Topic] = true;
            }

            if (!_subAckQueue.TryDequeue(out TestAckKind ackKind) || ackKind == TestAckKind.Success)
            {
                return Task.FromResult(new MqttClientSubscribeResult(0, new List<MqttClientSubscribeResultItem>() { new(new MqttTopicFilter() { Topic = options.TopicFilters.FirstOrDefault()!.Topic }, MqttClientSubscribeResultCode.GrantedQoS1) }, string.Empty, new List<MqttUserProperty>()));
            }
            else if (ackKind == TestAckKind.Fail)
            {
                return Task.FromResult(new MqttClientSubscribeResult(0, new List<MqttClientSubscribeResultItem>() { new(new MqttTopicFilter() { Topic = options.TopicFilters.FirstOrDefault()!.Topic }, MqttClientSubscribeResultCode.UnspecifiedError) }, string.Empty, new List<MqttUserProperty>()));
            }
            else if (ackKind == TestAckKind.Drop)
            {
                throw new MqttCommunicationException("Timed out waiting for a SubAck");
            }
            else
            {
                Assert.Fail($"unrecognized {nameof(TestAckKind)}: {ackKind}");
                return Task.FromResult<MqttClientSubscribeResult>(null!);
            }
        }

        public Task<MqttClientUnsubscribeResult> UnsubscribeAsync(MqttClientUnsubscribeOptions options, CancellationToken cancellationToken = default)
        {
            if (!_unsubAckQueue.TryDequeue(out TestAckKind ackKind) || ackKind == TestAckKind.Success)
            {
                return Task.FromResult(new MqttClientUnsubscribeResult(0, new List<MqttClientUnsubscribeResultItem>() { new(options.TopicFilters.FirstOrDefault()!, MqttClientUnsubscribeResultCode.Success) }, string.Empty, new List<MqttUserProperty>()));
            }
            else if (ackKind == TestAckKind.Fail)
            {
                return Task.FromResult(new MqttClientUnsubscribeResult(0, new List<MqttClientUnsubscribeResultItem>() { new(options.TopicFilters.FirstOrDefault()!, MqttClientUnsubscribeResultCode.UnspecifiedError) }, string.Empty, new List<MqttUserProperty>()));
            }
            else if (ackKind == TestAckKind.Drop)
            {
                throw new MqttCommunicationException("Timed out waiting for an UnsubAck");
            }
            else
            {
                Assert.Fail($"unrecognized {nameof(TestAckKind)}: {ackKind}");
                return Task.FromResult<MqttClientUnsubscribeResult>(null!);
            }
        }

        public void Dispose()
        {
        }

        internal void EnqueuePubAck(TestAckKind ackKind)
        {
            _pubAckQueue.Enqueue(ackKind);
        }

        internal void EnqueueSubAck(TestAckKind ackKind)
        {
            _subAckQueue.Enqueue(ackKind);
        }

        internal void EnqueueUnsubAck(TestAckKind ackKind)
        {
            _unsubAckQueue.Enqueue(ackKind);
        }

        internal async Task<ushort> ReceiveMessageAsync(MqttApplicationMessage appMsg, ushort? specificPacketId = null)
        {
            ushort packetId = specificPacketId != null ? (ushort)specificPacketId : (ushort)await _packetIdSequencer.Increment().ConfigureAwait(false);

            MqttApplicationMessageReceivedEventArgs msgReceivedArgs = new(
                Options.ClientId,
                appMsg,
                new MqttPublishPacket { PacketIdentifier = packetId },
                async (args, _) =>
                {
                    await _acknowledgementCount.Increment().ConfigureAwait(!false);
                    _ackedPacketIds.Enqueue(args.PacketIdentifier);
                });

            await ApplicationMessageReceivedAsync!.Invoke(msgReceivedArgs).WaitAsync(TestTimeout).ConfigureAwait(false);

            if (msgReceivedArgs.AutoAcknowledge)
            {
                await msgReceivedArgs.AcknowledgeAsync(CancellationToken.None).WaitAsync(TestTimeout).ConfigureAwait(false);
            }

            return packetId;
        }

        internal async Task<ushort> AwaitAcknowledgementAsync()
        {
            return await _ackedPacketIds.DequeueAsync().WaitAsync(TestTimeout).ConfigureAwait(false);
        }

        internal async Task<byte[]> AwaitPublishAsync()
        {
            return await _publishedCorrelationIds.DequeueAsync().WaitAsync(TestTimeout).ConfigureAwait(false);
        }

        internal bool HasSubscribed(string topic)
        {
            return _subscribedTopics.ContainsKey(topic);
        }

        internal MqttApplicationMessage? GetPublishedMessage(byte[]? correlationData)
        {
            if (!GuidExtensions.TryParseBytes(correlationData!, out Guid? correlationId))
            {
                correlationId = Guid.Empty;
            }

            return _publishedMessages.TryGetValue((Guid)correlationId!, out MqttApplicationMessage? publishedMessage) ? publishedMessage : null;
        }

        internal MqttApplicationMessage? GetPublishedMessage(int sequenceIndex)
        {
            return _publishedMessageSeq.TryGetValue(sequenceIndex, out MqttApplicationMessage? publishedMessage) ? publishedMessage : null;
        }

        public ValueTask DisposeAsync()
        {
            return new ValueTask();
        }

        public Task SendEnhancedAuthenticationExchangeDataAsync(MqttEnhancedAuthenticationExchangeData data, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
