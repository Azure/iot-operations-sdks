using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;
using MQTTnet.Exceptions;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace Azure.Iot.Operations.Protocol.UnitTests
{
    internal class MockMqttPubSubClient : IMqttPubSubClient
    {
        private static int ClientIdIndex;

        private readonly string _clientId;
        private readonly MqttProtocolVersion _protocolVersion;
        private readonly SemaphoreSlim _messageAcked = new(0);
        private int _mqttPacketId;
        private int _numSubscriptions;
        private int _numPublishes;
        private int _ackCount;
        
        public event Func<MqttApplicationMessageReceivedEventArgs, Task>? ApplicationMessageReceivedAsync;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public MockMqttPubSubClient(string? clientId = null, MqttProtocolVersion protocolVersion = MqttProtocolVersion.V500)
        {
            _clientId = clientId ?? $"MockClientIx{Interlocked.Increment(ref ClientIdIndex)}";
            _protocolVersion = protocolVersion;
        }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        public MqttProtocolVersion ProtocolVersion => _protocolVersion;

        public string ClientId => _clientId;

        public MqttApplicationMessage MessagePublished { get; set; }

        public List<MqttApplicationMessage> MessagesPublished { get; } = new();

        public string SubscribedTopicReceived { get; set; }

        public string UnsubscribeTopicReceived { get; set; }

        public int AcknowledgedMessageCount => _ackCount;

        public MqttQualityOfServiceLevel SubscribedTopicQoSReceived { get; private set; }

        public ConcurrentQueue<string> PacketsAcked { get; } = new();
        public ConcurrentQueue<string> PacketsPublished { get; } = new();

        public async Task SimulateNewMessage(MqttApplicationMessage msg, ushort packetId = default)
        {
            if (packetId == 0)
            {
                Interlocked.Increment(ref _mqttPacketId);
                packetId = (ushort)_mqttPacketId;
            }

            MqttApplicationMessageReceivedEventArgs msgReceivedArgs = new(
                ClientId,
                msg,
                packetId,
                AcknowledgeReceivedMessageAsync);

            Debug.Assert(ApplicationMessageReceivedAsync != null);
            await ApplicationMessageReceivedAsync.Invoke(msgReceivedArgs);

            if (msgReceivedArgs.AutoAcknowledge)
            {
                await AcknowledgeReceivedMessageAsync(msgReceivedArgs, CancellationToken.None);
            }
        }

        private Task AcknowledgeReceivedMessageAsync(MqttApplicationMessageReceivedEventArgs arg, CancellationToken ct)
        {
            if (arg.ApplicationMessage.UserProperties!.TryGetProperty("_failFirstPubAck", out string? failPubAckString)
                && (bool.TryParse(failPubAckString, out bool failPubAckBool))
                && failPubAckBool
                && arg.ApplicationMessage.QualityOfServiceLevel != MqttQualityOfServiceLevel.AtMostOnce)
            {
                var subListOfProperties = new List<MqttUserProperty>();
                foreach (var userProperty in arg.ApplicationMessage.UserProperties!)
                {
                    if (!userProperty.Name.Equals("_failFirstPubAck"))
                    {
                        subListOfProperties.Add(userProperty);
                    }
                }

                // For some reason, the arg.ApplicationMessage.UserProperties.Remove(...) call isn't actually removing anything so we have to just create a new list with all the previous properties other than the fault injection flag
                arg.ApplicationMessage.UserProperties = subListOfProperties;

                // Start a task to replay the message but remove the user property that causes the puback failure
                _ = SimulateNewMessage(arg.ApplicationMessage);
            }
            else
            {
                Interlocked.Increment(ref _ackCount);
                if (arg.ApplicationMessage.CorrelationData != null)
                {
                    PacketsAcked.Enqueue(Encoding.UTF8.GetString(arg.ApplicationMessage.CorrelationData));
                }
                _messageAcked.Release();
            }

            return Task.CompletedTask;
        }

        public Task SimulatedMessageAcknowledged(CancellationToken cancellationToken = default)
        {
            return _messageAcked.WaitAsync(cancellationToken);
        }

        public Task<MqttClientSubscribeResult> SubscribeAsync(MqttClientSubscribeOptions options, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _numSubscriptions);

            string topic = options.TopicFilters.FirstOrDefault()!.Topic;
            SubscribedTopicReceived = topic;
            SubscribedTopicQoSReceived = options.TopicFilters.FirstOrDefault()!.QualityOfServiceLevel;

            MqttClientSubscribeResult subAck = new(
                0,
                new List<MqttClientSubscribeResultItem>()
                {
                    new(new MqttTopicFilter(topic, MqttQualityOfServiceLevel.AtLeastOnce), MqttClientSubscribeReasonCode.GrantedQoS1)
                },
                string.Empty,
                new List<MqttUserProperty>());

            if (topic.EndsWith("/subAckUnspecifiedError"))
            {
                subAck = new(
                0,
                new List<MqttClientSubscribeResultItem>()
                {
                    new(new MqttTopicFilter(topic, MqttQualityOfServiceLevel.AtLeastOnce), MqttClientSubscribeReasonCode.UnspecifiedError)
                },
                string.Empty,
                new List<MqttUserProperty>());
            }

            return Task.FromResult(subAck);
        }

        public Task<MqttClientUnsubscribeResult> UnsubscribeAsync(MqttClientUnsubscribeOptions options, CancellationToken cancellationToken = default)
        {
            Interlocked.Decrement(ref _numSubscriptions);

            string topic = options.TopicFilters.FirstOrDefault()!;

            if (topic.EndsWith("/unsubAckUnspecifiedError"))
            {
                return Task.FromResult(
                new MqttClientUnsubscribeResult(
                    0,
                    new List<MqttClientUnsubscribeResultItem>()
                    {
                        new(topic, MqttClientUnsubscribeReasonCode.UnspecifiedError)
                    },
                    string.Empty,
                    new List<MqttUserProperty>()));
            }

            return Task.FromResult(
                new MqttClientUnsubscribeResult(
                    0,
                    new List<MqttClientUnsubscribeResultItem>()
                    {
                        new(topic, MqttClientUnsubscribeReasonCode.Success)
                    },
                    string.Empty,
                    new List<MqttUserProperty>()));
        }

        public Task<MqttClientPublishResult> PublishAsync(MqttApplicationMessage applicationMessage, CancellationToken cancellationToken = default)
        {
            MessagePublished = applicationMessage;
            MessagesPublished.Add(applicationMessage);
            Interlocked.Increment(ref _numPublishes);

            if (applicationMessage.CorrelationData != null)
            {
                PacketsPublished.Enqueue(Encoding.UTF8.GetString(applicationMessage.CorrelationData));
            }

            if (applicationMessage.UserProperties!.TryGetProperty("_failPubAck", out string? failPubAck) && failPubAck == "true")
            {
                if (applicationMessage.UserProperties!.TryGetProperty("_notAuthorized", out string? naPubAck) && naPubAck == "true")
                {
                    return Task.FromResult(new MqttClientPublishResult(0, MqttClientPublishReasonCode.NotAuthorized, string.Empty, new List<MqttUserProperty>()));
                }
                else if (applicationMessage.UserProperties!.TryGetProperty("_noMatchingSubscribers", out string? nmsPubAck) && nmsPubAck == "true")
                {
                    return Task.FromResult(new MqttClientPublishResult(0, MqttClientPublishReasonCode.NoMatchingSubscribers, string.Empty, new List<MqttUserProperty>()));
                }
                else
                {
                    return Task.FromResult(new MqttClientPublishResult(0, MqttClientPublishReasonCode.UnspecifiedError, string.Empty, new List<MqttUserProperty>()));
                }
            }

            if (applicationMessage.UserProperties!.TryGetProperty("_dropPubAck", out string? dropPubAck) && dropPubAck == "true")
            {
                throw new MqttCommunicationException("PubAck dropped");
            }

            string topic = applicationMessage.Topic;
            if (topic.Contains("failPubAck"))
            {
                if (topic.Contains("notAuthorized"))
                {
                    return Task.FromResult(new MqttClientPublishResult(0, MqttClientPublishReasonCode.NotAuthorized, string.Empty, new List<MqttUserProperty>()));
                }
                else if (topic.Contains("noMatchingSubscribers"))
                {
                    return Task.FromResult(new MqttClientPublishResult(0, MqttClientPublishReasonCode.NoMatchingSubscribers, string.Empty, new List<MqttUserProperty>()));
                }
                else
                {
                    return Task.FromResult(new MqttClientPublishResult(0, MqttClientPublishReasonCode.UnspecifiedError, string.Empty, new List<MqttUserProperty>()));
                }
            }
            else if (topic.Contains("dropPubAck"))
            {
                throw new MqttCommunicationException("PubAck dropped");
            }

            return Task.FromResult(new MqttClientPublishResult(0, MqttClientPublishReasonCode.Success, string.Empty, new List<MqttUserProperty>()));
        }

        public int GetNumberOfPublishes()
        {
            return _numPublishes;
        }

        public ValueTask DisposeAsync(bool disposing)
        {
            _messageAcked.Dispose();
            return new ValueTask();
        }

        public ValueTask DisposeAsync()
        {
            _messageAcked.Dispose();
            return new ValueTask();
        }
    }
}