// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Text;
using Microsoft.VisualStudio.Threading;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Diagnostics;
using MQTTnet.Exceptions;
using MQTTnet.Formatter;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using Azure.Iot.Operations.Protocol;
using Xunit;

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    internal class StubMqttClient : MQTTnet.Client.IMqttClient
    {
        private static readonly TimeSpan TestTimeout = TimeSpan.FromMinutes(1);

        private static readonly MqttConnAckPacket SuccessfulReconnectConnAck = new()
        {
            ReasonCode = MqttConnectReasonCode.Success,
            IsSessionPresent = true,
        };

        private AsyncAtomicInt packetIdSequencer;
        private AsyncQueue<TestAckKind> pubAckQueue;
        private AsyncQueue<TestAckKind> subAckQueue;
        private AsyncQueue<TestAckKind> unsubAckQueue;
        private AsyncQueue<ushort> ackedPacketIds;
        private AsyncAtomicInt publicationCount;
        private AsyncAtomicInt acknowledgementCount;
        private AsyncQueue<byte[]> publishedCorrelationIds;
        private ConcurrentDictionary<string, bool> subscribedTopics;
        private ConcurrentDictionary<Guid, MqttApplicationMessage> publishedMessages;
        private ConcurrentDictionary<int, MqttApplicationMessage> publishedMessageSeq;

        public StubMqttClient(string clientId)
        {
            ApplicationMessageReceivedAsync = _ => Task.CompletedTask;
            ConnectedAsync = _ => Task.CompletedTask;
            ConnectingAsync = _ => Task.CompletedTask;
            DisconnectedAsync = _ => Task.CompletedTask;
            InspectPacketAsync = _ => Task.CompletedTask;
            IsConnected = false;

            Options = new MqttClientOptions() { ClientId = clientId, ProtocolVersion = MqttProtocolVersion.V500 };

            packetIdSequencer = new(0);
            pubAckQueue = new();
            subAckQueue = new();
            unsubAckQueue = new();
            ackedPacketIds = new();
            publicationCount = new(0);
            acknowledgementCount = new(0);
            publishedCorrelationIds = new();
            subscribedTopics = new();
            publishedMessages = new();
            publishedMessageSeq = new();
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
            return await publicationCount.Read().ConfigureAwait(false);
        }

        internal async Task<int> GetAcknowledgementCount()
        {
            return await acknowledgementCount.Read().ConfigureAwait(false);
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
            int sequenceIndex = await publicationCount.Increment().ConfigureAwait(false) - 1;

            publishedMessageSeq.TryAdd(sequenceIndex, applicationMessage);

            if (!GuidExtensions.TryParseBytes(applicationMessage.CorrelationData ?? Array.Empty<byte>(), out Guid? correlationId))
            {
                correlationId = Guid.Empty;
            }

            publishedMessages.TryAdd((Guid)correlationId!, applicationMessage);
            publishedCorrelationIds.Enqueue(applicationMessage.CorrelationData ?? Array.Empty<byte>());

            if (!pubAckQueue.TryDequeue(out TestAckKind ackKind) || ackKind == TestAckKind.Success)
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

        public Task SendExtendedAuthenticationExchangeDataAsync(MqttExtendedAuthenticationExchangeData data, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<MqttClientSubscribeResult> SubscribeAsync(MqttClientSubscribeOptions options, CancellationToken cancellationToken = default)
        {
            foreach (MqttTopicFilter topicFilter in options.TopicFilters)
            {
                subscribedTopics[topicFilter.Topic] = true;
            }

            if (!subAckQueue.TryDequeue(out TestAckKind ackKind) || ackKind == TestAckKind.Success)
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
            if (!unsubAckQueue.TryDequeue(out TestAckKind ackKind) || ackKind == TestAckKind.Success)
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
            pubAckQueue.Enqueue(ackKind);
        }

        internal void EnqueueSubAck(TestAckKind ackKind)
        {
            subAckQueue.Enqueue(ackKind);
        }

        internal void EnqueueUnsubAck(TestAckKind ackKind)
        {
            unsubAckQueue.Enqueue(ackKind);
        }

        internal async Task<ushort> ReceiveMessageAsync(MqttApplicationMessage appMsg, ushort? specificPacketId = null)
        {
            ushort packetId = specificPacketId != null ? (ushort)specificPacketId : (ushort)await packetIdSequencer.Increment().ConfigureAwait(false);

            MqttApplicationMessageReceivedEventArgs msgReceivedArgs = new(
                Options.ClientId,
                appMsg,
                new MqttPublishPacket { PacketIdentifier = packetId },
                async (args, _) =>
                {
                    await acknowledgementCount.Increment().ConfigureAwait(!false);
                    ackedPacketIds.Enqueue(args.PacketIdentifier);
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
            return await ackedPacketIds.DequeueAsync().WaitAsync(TestTimeout).ConfigureAwait(false);
        }

        internal async Task<byte[]> AwaitPublishAsync()
        {
            return await publishedCorrelationIds.DequeueAsync().WaitAsync(TestTimeout).ConfigureAwait(false);
        }

        internal bool HasSubscribed(string topic)
        {
            return subscribedTopics.ContainsKey(topic);
        }

        internal MqttApplicationMessage? GetPublishedMessage(byte[]? correlationData)
        {
            if (!GuidExtensions.TryParseBytes(correlationData!, out Guid? correlationId))
            {
                correlationId = Guid.Empty;
            }

            return publishedMessages.TryGetValue((Guid)correlationId!, out MqttApplicationMessage? publishedMessage) ? publishedMessage : null;
        }

        internal MqttApplicationMessage? GetPublishedMessage(int sequenceIndex)
        {
            return publishedMessageSeq.TryGetValue(sequenceIndex, out MqttApplicationMessage? publishedMessage) ? publishedMessage : null;
        }

        public ValueTask DisposeAsync(bool disposing)
        {
            return new ValueTask();
        }

        public ValueTask DisposeAsync()
        {
            return new ValueTask();
        }
    }
}