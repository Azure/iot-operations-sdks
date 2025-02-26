// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;
using MQTTnet;
using MQTTnet.Diagnostics.PacketInspection;
using MQTTnet.Formatter;
using MQTTnet.Packets;
using MQTTnet.Protocol;

namespace Azure.Iot.Operations.Protocol.UnitTests
{
    public class MockMqttClient : MQTTnet.IMqttClient
    {
        private string _clientId;
        private readonly MqttProtocolVersion _protocolVersion;
        private bool _isConnected;

        public static MqttConnAckPacket SuccessfulInitialConnAck = new()
        {
            ReasonCode = MqttConnectReasonCode.Success,
            IsSessionPresent = false,
        };

        public static MqttConnAckPacket SuccessfulReconnectConnAck = new()
        {
            ReasonCode = MqttConnectReasonCode.Success,
            IsSessionPresent = true,
        };

        public static MqttConnAckPacket UnsuccessfulReconnectConnAck = new()
        {
            ReasonCode = MqttConnectReasonCode.ServerBusy,
        };

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public MockMqttClient(string? clientId = null, MqttProtocolVersion protocolVersion = MqttProtocolVersion.V500)
        {
            _clientId = clientId ?? Guid.NewGuid().ToString();
            _protocolVersion = protocolVersion;
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
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        public bool IsConnected => _isConnected;

        public MqttClientOptions Options { get; set; }

        public List<MqttApplicationMessageReceivedEventArgs> AcknowledgedMessages = new();

#pragma warning disable CS0067
        public event Func<MqttClientConnectedEventArgs, Task> ConnectedAsync;
        public event Func<MqttClientConnectingEventArgs, Task> ConnectingAsync;
        public event Func<MqttApplicationMessageReceivedEventArgs, Task> ApplicationMessageReceivedAsync;
        public event Func<InspectMqttPacketEventArgs, Task> InspectPackage;
        public event Func<InspectMqttPacketEventArgs, Task> InspectPacketAsync;
#pragma warning restore CS0067

        public event Func<MqttClientDisconnectedEventArgs, Task> DisconnectedAsync;

        public event Func<MqttClientOptions, Task<MqttClientConnectResult>>? OnConnectAttempt;

        public event Func<MqttClientDisconnectOptions, Task>? OnDisconnectAttempt;

        public event Func<MqttApplicationMessage, Task<MqttClientPublishResult>>? OnPublishAttempt;

        public event Func<MqttClientSubscribeOptions, Task<MqttClientSubscribeResult>>? OnSubscribeAttempt;

        public event Func<MqttClientUnsubscribeOptions, Task<MqttClientUnsubscribeResult>>? OnUnsubscribeAttempt;

        public event Func<MqttApplicationMessage, Task>? OnPublishAcknowledged;


        public async Task SimulateNewMessageAsync(MqttApplicationMessage msg, ushort packetId = default)
        {
            MqttApplicationMessageReceivedEventArgs msgReceivedArgs = new(
                Options.ClientId,
                msg,
                new MqttPublishPacket { PacketIdentifier = packetId },
                AcknowledgeReceivedMessageAsync);

            await ApplicationMessageReceivedAsync.Invoke(msgReceivedArgs);

            if (msgReceivedArgs.AutoAcknowledge)
            {
                await AcknowledgeReceivedMessageAsync(msgReceivedArgs, CancellationToken.None);
            }
        }

        public async Task SimulateServerInitiatedDisconnectAsync(Exception cause, MqttClientDisconnectReason reason = MqttClientDisconnectReason.ImplementationSpecificError)
        {
            _isConnected = false;

            if (DisconnectedAsync != null)
            {
                await DisconnectedAsync.Invoke(new MqttClientDisconnectedEventArgs(true, new MqttClientConnectResult(), reason, "simulated test disconnect", new List<MqttUserProperty>(), cause));
            }
        }

        private Task AcknowledgeReceivedMessageAsync(MqttApplicationMessageReceivedEventArgs msgReceivedArgs, CancellationToken none)
        {
            AcknowledgedMessages.Add(msgReceivedArgs);
            _ = OnPublishAcknowledged?.Invoke(msgReceivedArgs.ApplicationMessage);
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken token = default)
        {
            return DisconnectAsync(new MqttClientDisconnectOptions(), token);
        }

        public async Task<MqttClientConnectResult> ConnectAsync(MqttClientOptions options, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            MqttClientConnectResult connectResult;
            if (OnConnectAttempt != null)
            {
                connectResult = await OnConnectAttempt(options);
            }
            else
            {
                var connAckPacket = new MqttConnAckPacket()
                {
                    ReasonCode = MqttConnectReasonCode.Success,
                    IsSessionPresent = !options.CleanSession,
                };

                connectResult = new MqttClientConnectResultFactory().Create(connAckPacket, _protocolVersion);
            }

            if (connectResult.ResultCode == MqttClientConnectResultCode.Success)
            {
                _isConnected = true;
            }

            Options = options;

            // This partiular block mimics how MQTTnet's MQTT client will locally save any service-returned CONNACK fields
            // that are relevant to the client
            if (connectResult.AssignedClientIdentifier != null)
            {
                _clientId = connectResult.AssignedClientIdentifier;
                Options.ClientId = connectResult.AssignedClientIdentifier;
            }
            else if (string.IsNullOrEmpty(options.ClientId))
            {
                options.ClientId = Guid.NewGuid().ToString();
            }

            _ = ConnectedAsync?.Invoke(new MqttClientConnectedEventArgs(connectResult));

            return connectResult;
        }

        public async Task DisconnectAsync(MqttClientDisconnectOptions options, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (OnDisconnectAttempt != null)
            {
                await OnDisconnectAttempt(options);
            }

            _isConnected = false;

            DisconnectedAsync?.Invoke(new MqttClientDisconnectedEventArgs(true, new MqttClientConnectResult(), MqttClientDisconnectReason.NormalDisconnection, "disconnected", new List<MqttUserProperty>(), new Exception()));
        }

        public Task PingAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public async Task<MqttClientSubscribeResult> SubscribeAsync(MqttClientSubscribeOptions options, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (OnSubscribeAttempt != null)
            {
                return await OnSubscribeAttempt.Invoke(options);
            }

            List<MqttClientSubscribeResultItem> results = new();
            foreach (MqttTopicFilter filter in options.TopicFilters)
            {
                if (filter.QualityOfServiceLevel == MqttQualityOfServiceLevel.AtMostOnce)
                {
                    results.Add(new MqttClientSubscribeResultItem(filter, MqttClientSubscribeResultCode.GrantedQoS0));
                }
                else if (filter.QualityOfServiceLevel == MqttQualityOfServiceLevel.AtLeastOnce)
                {
                    results.Add(new MqttClientSubscribeResultItem(filter, MqttClientSubscribeResultCode.GrantedQoS1));
                }
                else
                {
                    results.Add(new MqttClientSubscribeResultItem(filter, MqttClientSubscribeResultCode.GrantedQoS2));
                }
            }

            return new MqttClientSubscribeResult(0, results, "", new List<MqttUserProperty>());
        }

        public async Task<MqttClientUnsubscribeResult> UnsubscribeAsync(MqttClientUnsubscribeOptions options, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (OnUnsubscribeAttempt != null)
            {
                return await OnUnsubscribeAttempt.Invoke(options);
            }

            List<MqttClientUnsubscribeResultItem> results = new();
            foreach (string topic in options.TopicFilters)
            {
                results.Add(new MqttClientUnsubscribeResultItem(topic, MqttClientUnsubscribeResultCode.Success));
            }

            return new MqttClientUnsubscribeResult(0, results, "", new List<MqttUserProperty>());
        }

        public Task<MqttClientPublishResult> PublishAsync(MqttApplicationMessage applicationMessage, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (OnPublishAttempt != null)
            {
                return OnPublishAttempt.Invoke(applicationMessage);
            }

            return Task.FromResult(new MqttClientPublishResult(0, MqttClientPublishReasonCode.Success, string.Empty, null));
        }

        public void Dispose()
        {
            // nothing to dispose
        }


        // Returns a successful result only if the expected matches the actual. Throws with a human-readable error message
        // that explains the difference if there was one.
        public MqttClientConnectResult CompareExpectedConnectWithActual(Azure.Iot.Operations.Protocol.Models.MqttClientOptions expectedOptions, MqttClientOptions actualOptions, bool isReconnecting)
        {
            CompareExpectedUserPropertiesWithActual(expectedOptions.UserProperties, actualOptions.UserProperties);

            if (expectedOptions.AllowPacketFragmentation != actualOptions.AllowPacketFragmentation)
            {
                throw new InvalidOperationException("The AllowPacketFragmentation value of the connect did not propagate down to the underlying mqtt client's connect request");
            }
            else if (!Enumerable.Equals(expectedOptions.AuthenticationData, actualOptions.AuthenticationData))
            {
                throw new InvalidOperationException("The AuthenticationData value of the connect did not propagate down to the underlying mqtt client's connect request");
            }
            else if (!string.Equals(expectedOptions.AuthenticationMethod, actualOptions.AuthenticationMethod))
            {
                throw new InvalidOperationException("The AuthenticationMethod value of the connect did not propagate down to the underlying mqtt client's connect request");
            }
            else if (isReconnecting && actualOptions.CleanSession)
            {
                // This is a special case because the session client will override the clean session flag to be true when first connecting and will override it to be false when reconnecting.
                // Because of this, don't bother checking what the expectedOptions value here was.
                throw new InvalidOperationException("The CleanSession value when reconnecting should be false");
            }
            else if (!isReconnecting && !actualOptions.CleanSession)
            {
                // This is a special case because the session client will override the clean session flag to be true when first connecting and will override it to be false when reconnecting.
                // A user
                throw new InvalidOperationException("The CleanSession value when first connecting should be true");
            }
            else if (!string.Equals(expectedOptions.ClientId, actualOptions.ClientId))
            {
                throw new InvalidOperationException("The ClientId value of the connect did not propagate down to the underlying mqtt client's connect request");
            }
            else if (expectedOptions.KeepAlivePeriod.CompareTo(actualOptions.KeepAlivePeriod) != 0)
            {
                throw new InvalidOperationException("The KeepAlivePeriod value of the connect did not propagate down to the underlying mqtt client's connect request");
            }
            else if (expectedOptions.MaximumPacketSize != actualOptions.MaximumPacketSize)
            {
                throw new InvalidOperationException("The MaximumPacketSize value of the connect did not propagate down to the underlying mqtt client's connect request");
            }
            else if ((int)expectedOptions.ProtocolVersion != (int)actualOptions.ProtocolVersion)
            {
                throw new InvalidOperationException("The ProtocolVersion value of the connect did not propagate down to the underlying mqtt client's connect request");
            }
            else if (expectedOptions.ReceiveMaximum != actualOptions.ReceiveMaximum)
            {
                throw new InvalidOperationException("The ReceiveMaximum value of the connect did not propagate down to the underlying mqtt client's connect request");
            }
            else if (expectedOptions.RequestProblemInformation != actualOptions.RequestProblemInformation)
            {
                throw new InvalidOperationException("The RequestProblemInformation value of the connect did not propagate down to the underlying mqtt client's connect request");
            }
            else if (expectedOptions.RequestResponseInformation != actualOptions.RequestResponseInformation)
            {
                throw new InvalidOperationException("The RequestResponseInformation value of the connect did not propagate down to the underlying mqtt client's connect request");
            }
            else if (expectedOptions.SessionExpiryInterval != actualOptions.SessionExpiryInterval)
            {
                throw new InvalidOperationException("The SessionExpiryInterval value of the connect did not propagate down to the underlying mqtt client's connect request");
            }
            else if (expectedOptions.Timeout.CompareTo(actualOptions.Timeout) != 0)
            {
                throw new InvalidOperationException("The Timeout value of the connect did not propagate down to the underlying mqtt client's connect request");
            }
            else if (expectedOptions.TopicAliasMaximum != actualOptions.TopicAliasMaximum)
            {
                throw new InvalidOperationException("The TopicAliasMaximum value of the connect did not propagate down to the underlying mqtt client's connect request");
            }
            else if (expectedOptions.TryPrivate != actualOptions.TryPrivate)
            {
                throw new InvalidOperationException("The TryPrivate value of the connect did not propagate down to the underlying mqtt client's connect request");
            }
            else if (expectedOptions.ValidateFeatures != actualOptions.ValidateFeatures)
            {
                throw new InvalidOperationException("The ValidateFeatures value of the connect did not propagate down to the underlying mqtt client's connect request");
            }
            else if (expectedOptions.WriterBufferSize != actualOptions.WriterBufferSize)
            {
                throw new InvalidOperationException("The WriterBufferSize value of the connect did not propagate down to the underlying mqtt client's connect request");
            }
            else if (expectedOptions.WriterBufferSizeMax != actualOptions.WriterBufferSizeMax)
            {
                throw new InvalidOperationException("The WriterBufferSizeMax value of the connect did not propagate down to the underlying mqtt client's connect request");
            }
            else if (!string.Equals(expectedOptions.WillContentType, actualOptions.WillContentType)
                || !Enumerable.Equals(expectedOptions.WillCorrelationData, actualOptions.WillCorrelationData)
                || expectedOptions.WillDelayInterval != actualOptions.WillDelayInterval
                || expectedOptions.WillMessageExpiryInterval != actualOptions.WillMessageExpiryInterval
                || !Enumerable.Equals(expectedOptions.WillPayload, actualOptions.WillPayload)
                || (int)expectedOptions.WillPayloadFormatIndicator != (int)actualOptions.WillPayloadFormatIndicator
                || (int)expectedOptions.WillQualityOfServiceLevel != (int)actualOptions.WillQualityOfServiceLevel
                || !string.Equals(expectedOptions.WillResponseTopic, actualOptions.WillResponseTopic)
                || expectedOptions.WillRetain != actualOptions.WillRetain
                || !string.Equals(expectedOptions.WillTopic, actualOptions.WillTopic))
            {
                throw new InvalidOperationException("The Will message values of the connect did not propagate down to the underlying mqtt client's connect request");
            }

            CompareExpectedUserPropertiesWithActual(expectedOptions.WillUserProperties, actualOptions.WillUserProperties);

            return new MqttClientConnectResultFactory().Create(expectedOptions.CleanSession ? SuccessfulInitialConnAck : SuccessfulReconnectConnAck, _protocolVersion);
        }

        // Returns a successful result only if the expected matches the actual. Throws with a human-readable error message
        // that explains the difference if there was one.
        public static void CompareExpectedDisconnectWithActual(Azure.Iot.Operations.Protocol.Models.MqttClientDisconnectOptions expectedOptions, MqttClientDisconnectOptions actualOptions)
        {
            CompareExpectedUserPropertiesWithActual(expectedOptions.UserProperties, actualOptions.UserProperties);

            if ((int)expectedOptions.Reason != (int)actualOptions.Reason)
            {
                throw new InvalidOperationException("The Reason value of the disconnect did not propagate down to the underlying mqtt client's connect request");
            }
            else if (!string.Equals(expectedOptions.ReasonString, actualOptions.ReasonString))
            {
                throw new InvalidOperationException("The ReasonString value of the disconnect did not propagate down to the underlying mqtt client's connect request");
            }
            else if (expectedOptions.SessionExpiryInterval != actualOptions.SessionExpiryInterval)
            {
                throw new InvalidOperationException("The SessionExpiryInterval value of the disconnect did not propagate down to the underlying mqtt client's connect request");
            }
        }

        // Returns a successful result only if the expected matches the actual. Throws with a human-readable error message
        // that explains the difference if there was one.
        public static MqttClientPublishResult CompareExpectedPublishWithActual(Azure.Iot.Operations.Protocol.Models.MqttApplicationMessage expectedMessage, MQTTnet.MqttApplicationMessage actualMessage)
        {
            // Verify that the message published by the mqtt client matches the message that the session client published
            if (!Enumerable.SequenceEqual(actualMessage.Payload.ToArray(), expectedMessage.Payload.ToArray()))
            {
                throw new InvalidOperationException("The payload of the publish did not propagate down to the underlying mqtt client's publish request");
            }
            else if (actualMessage.Payload.Length != expectedMessage.Payload.Length)
            {
                throw new InvalidOperationException("The size of the payload did not propagate down to the underlying mqtt client's publish request");
            }
            else if (!string.Equals(actualMessage.ContentType, expectedMessage.ContentType))
            {
                throw new InvalidOperationException("The content type of the publish did not propagate down to the underlying mqtt client's publish request");
            }
            else if (actualMessage.CorrelationData == null && expectedMessage.CorrelationData != null
                || actualMessage.CorrelationData != null && expectedMessage.CorrelationData == null)
            {
                throw new InvalidOperationException("The correlation data of the publish did not propagate down to the underlying mqtt client's publish request");
            }
            else if (actualMessage.CorrelationData != null && expectedMessage.CorrelationData != null
                && !Enumerable.SequenceEqual(actualMessage.CorrelationData, expectedMessage.CorrelationData))
            {
                throw new InvalidOperationException("The correlation data of the publish did not propagate down to the underlying mqtt client's publish request");
            }
            else if (!string.Equals(actualMessage.MessageExpiryInterval, expectedMessage.MessageExpiryInterval))
            {
                throw new InvalidOperationException("The message expiry interval of the publish did not propagate down to the underlying mqtt client's publish request");
            }
            else if (!int.Equals((int)actualMessage.PayloadFormatIndicator, (int)expectedMessage.PayloadFormatIndicator))
            {
                throw new InvalidOperationException("The payload format indicator of the publish did not propagate down to the underlying mqtt client's publish request");
            }
            else if (!int.Equals((int)actualMessage.QualityOfServiceLevel, (int)expectedMessage.QualityOfServiceLevel))
            {
                throw new InvalidOperationException("The QoS of the publish did not propagate down to the underlying mqtt client's publish request");
            }
            else if (!string.Equals(actualMessage.ResponseTopic, expectedMessage.ResponseTopic))
            {
                throw new InvalidOperationException("The response topic of the publish did not propagate down to the underlying mqtt client's publish request");
            }
            else if (!bool.Equals(actualMessage.Retain, expectedMessage.Retain))
            {
                throw new InvalidOperationException("The retain flag value of the publish did not propagate down to the underlying mqtt client's publish request");
            }
            else if ((actualMessage.SubscriptionIdentifiers == null) != (expectedMessage.SubscriptionIdentifiers == null))
            {
                throw new InvalidOperationException("The subscription identifiers of the publish did not propagate down to the underlying mqtt client's publish request");
            }
            else if (actualMessage.SubscriptionIdentifiers != null && expectedMessage.SubscriptionIdentifiers != null)
            {
                if ((!int.Equals(actualMessage.SubscriptionIdentifiers.Count, expectedMessage.SubscriptionIdentifiers.Count)
                    || !Enumerable.SequenceEqual(actualMessage.SubscriptionIdentifiers, expectedMessage.SubscriptionIdentifiers)))
                {
                    throw new InvalidOperationException("The subscription identifiers of the publish did not propagate down to the underlying mqtt client's publish request");
                }
            }
            else if (!string.Equals(actualMessage.Topic, expectedMessage.Topic))
            {
                throw new InvalidOperationException("The topic of the publish did not propagate down to the underlying mqtt client's publish request");
            }
            else if (!string.Equals(actualMessage.TopicAlias, expectedMessage.TopicAlias))
            {
                throw new InvalidOperationException("The topic alias of the publish did not propagate down to the underlying mqtt client's publish request");
            }

            CompareExpectedUserPropertiesWithActual(expectedMessage.UserProperties, actualMessage.UserProperties);

            return new MQTTnet.MqttClientPublishResult(0, MQTTnet.MqttClientPublishReasonCode.Success, "", new List<MQTTnet.Packets.MqttUserProperty>());
        }

        // Returns a successful result only if the expected matches the actual. Throws with a human-readable error message
        // that explains the difference if there was one.
        public static MqttClientSubscribeResult CompareExpectedSubscribeWithActual(Azure.Iot.Operations.Protocol.Models.MqttClientSubscribeOptions expectedOptions, MQTTnet.MqttClientSubscribeOptions actualOptions)
        {
            if (!int.Equals(expectedOptions.TopicFilters.Count, actualOptions.TopicFilters.Count))
            {
                throw new InvalidOperationException("The expected number of topic filters did not propagate down to the underlying mqtt client's subscribe request");
            }

            if (actualOptions.SubscriptionIdentifier != expectedOptions.SubscriptionIdentifier)
            {
                throw new InvalidOperationException($"The subscription identifier did not propagate down to the underlying mqtt client's subscribe request");
            }

            CompareExpectedUserPropertiesWithActual(expectedOptions.UserProperties, actualOptions.UserProperties);

            List<MqttClientSubscribeResultItem> results = new();
            foreach (MqttTopicFilter actualFilter in actualOptions.TopicFilters)
            {
                bool foundMatchingFilter = false;
                foreach (Azure.Iot.Operations.Protocol.Models.MqttTopicFilter expectedFilter in expectedOptions.TopicFilters)
                {
                    if (string.Equals(expectedFilter.Topic, actualFilter.Topic))
                    {
                        if (expectedFilter.NoLocal != actualFilter.NoLocal)
                        {
                            throw new InvalidOperationException($"The filter for topic {expectedFilter.Topic} did not propagate the NoLocal value down to the underlying mqtt client's subscribe request");
                        }
                        else if (expectedFilter.RetainAsPublished != actualFilter.RetainAsPublished)
                        {
                            throw new InvalidOperationException($"The filter for topic {expectedFilter.Topic} did not propagate the RetainAsPublished value down to the underlying mqtt client's subscribe request");
                        }
                        else if ((int)expectedFilter.RetainHandling != (int)actualFilter.RetainHandling)
                        {
                            throw new InvalidOperationException($"The filter for topic {expectedFilter.Topic} did not propagate the RetainHandling value down to the underlying mqtt client's subscribe request");
                        }
                        else if ((int)expectedFilter.QualityOfServiceLevel != (int)actualFilter.QualityOfServiceLevel)
                        {
                            throw new InvalidOperationException($"The filter for topic {expectedFilter.Topic} did not propagate the QoS value down to the underlying mqtt client's subscribe request");
                        }

                        foundMatchingFilter = true;
                        break;
                    }
                }

                if (!foundMatchingFilter)
                {
                    throw new InvalidOperationException($"Unexpected filter with topic {actualFilter.Topic} was propagated down to the underlying mqtt client's subscribe request");
                }

                if (actualFilter.QualityOfServiceLevel == MqttQualityOfServiceLevel.AtMostOnce)
                {
                    results.Add(new MqttClientSubscribeResultItem(actualFilter, MqttClientSubscribeResultCode.GrantedQoS0));
                }
                else if (actualFilter.QualityOfServiceLevel == MqttQualityOfServiceLevel.AtLeastOnce)
                {
                    results.Add(new MqttClientSubscribeResultItem(actualFilter, MqttClientSubscribeResultCode.GrantedQoS1));
                }
                else
                {
                    results.Add(new MqttClientSubscribeResultItem(actualFilter, MqttClientSubscribeResultCode.GrantedQoS2));
                }
            }

            return new MQTTnet.MqttClientSubscribeResult(0, results, "", new List<MQTTnet.Packets.MqttUserProperty>());
        }

        // Returns a successful result only if the expected matches the actual. Throws with a human-readable error message
        // that explains the difference if there was one.
        public static MqttClientUnsubscribeResult CompareExpectedUnsubscribeWithActual(Azure.Iot.Operations.Protocol.Models.MqttClientUnsubscribeOptions expectedOptions, MqttClientUnsubscribeOptions actualOptions)
        {
            if (!int.Equals(expectedOptions.TopicFilters.Count, actualOptions.TopicFilters.Count))
            {
                throw new InvalidOperationException("The expected number of topic filters did not propagate down to the underlying mqtt client's unsubscribe request");
            }

            CompareExpectedUserPropertiesWithActual(expectedOptions.UserProperties, actualOptions.UserProperties);

            List<MqttClientUnsubscribeResultItem> results = new();
            foreach (string actualFilter in actualOptions.TopicFilters)
            {
                bool foundMatchingFilter = false;
                foreach (string expectedFilter in expectedOptions.TopicFilters)
                {
                    if (string.Equals(expectedFilter, actualFilter))
                    {
                        foundMatchingFilter = true;
                        break;
                    }
                }

                if (!foundMatchingFilter)
                {
                    throw new InvalidOperationException($"Unexpected filter with topic {actualFilter} was propagated down to the underlying mqtt client's unsubscribe request");
                }

                results.Add(new MqttClientUnsubscribeResultItem(actualFilter, MqttClientUnsubscribeResultCode.Success));
            }

            return new MqttClientUnsubscribeResult(0, results, "", new List<MqttUserProperty>());
        }

        public static void CompareExpectedReceivedPublishWithActual(MQTTnet.MqttApplicationMessage expected, ushort expectedPacketId, Azure.Iot.Operations.Protocol.Events.MqttApplicationMessageReceivedEventArgs actual)
        {
            if (actual.PacketIdentifier != expectedPacketId)
            {
                throw new InvalidOperationException("The packet Id of the publish did not propagate up from the underlying mqtt client's publish request");
            }
            else if (!Enumerable.SequenceEqual(actual.ApplicationMessage.Payload.ToArray(), expected.Payload.ToArray()))
            {
                throw new InvalidOperationException("The payload of the publish did not propagate up from the underlying mqtt client's publish request");
            }
            else if (!string.Equals(actual.ApplicationMessage.ContentType, expected.ContentType))
            {
                throw new InvalidOperationException("The content type of the publish did not propagate up from the underlying mqtt client's publish request");
            }
            else if (actual.ApplicationMessage.CorrelationData == null && expected.CorrelationData != null
                || actual.ApplicationMessage.CorrelationData != null && expected.CorrelationData == null)
            {
                throw new InvalidOperationException("The correlation data of the publish did not propagate up from the underlying mqtt client's publish request");
            }
            else if (actual.ApplicationMessage.CorrelationData != null && expected.CorrelationData != null
                && !Enumerable.SequenceEqual(actual.ApplicationMessage.CorrelationData, expected.CorrelationData))
            {
                throw new InvalidOperationException("The correlation data of the publish did not propagate up from the underlying mqtt client's publish request");
            }
            else if (!string.Equals(actual.ApplicationMessage.MessageExpiryInterval, expected.MessageExpiryInterval))
            {
                throw new InvalidOperationException("The message expiry interval of the publish did not propagate up from the underlying mqtt client's publish request");
            }
            else if (!int.Equals((int)actual.ApplicationMessage.PayloadFormatIndicator, (int)expected.PayloadFormatIndicator))
            {
                throw new InvalidOperationException("The payload format indicator of the publish did not propagate up from the underlying mqtt client's publish request");
            }
            else if (!int.Equals((int)actual.ApplicationMessage.QualityOfServiceLevel, (int)expected.QualityOfServiceLevel))
            {
                throw new InvalidOperationException("The QoS of the publish did not propagate up from the underlying mqtt client's publish request");
            }
            else if (!string.Equals(actual.ApplicationMessage.ResponseTopic, expected.ResponseTopic))
            {
                throw new InvalidOperationException("The response topic of the publish did not propagate up from the underlying mqtt client's publish request");
            }
            else if (!bool.Equals(actual.ApplicationMessage.Retain, expected.Retain))
            {
                throw new InvalidOperationException("The retain flag value of the publish did not propagate up from the underlying mqtt client's publish request");
            }
            else if ((actual.ApplicationMessage.SubscriptionIdentifiers == null) != (expected.SubscriptionIdentifiers == null))
            {
                throw new InvalidOperationException("The subscription identifiers of the publish did not propagate up from the underlying mqtt client's publish request");
            }
            else if (actual.ApplicationMessage.SubscriptionIdentifiers != null && expected.SubscriptionIdentifiers != null)
            {
                if ((!int.Equals(actual.ApplicationMessage.SubscriptionIdentifiers.Count, expected.SubscriptionIdentifiers.Count)
                    || !Enumerable.SequenceEqual(actual.ApplicationMessage.SubscriptionIdentifiers, expected.SubscriptionIdentifiers)))
                {
                    throw new InvalidOperationException("The subscription identifiers of the publish did not propagate up from the underlying mqtt client's publish request");
                }
            }
            else if (!string.Equals(actual.ApplicationMessage.Topic, expected.Topic))
            {
                throw new InvalidOperationException("The topic of the publish did not propagate up from the underlying mqtt client's publish request");
            }
            else if (!string.Equals(actual.ApplicationMessage.TopicAlias, expected.TopicAlias))
            {
                throw new InvalidOperationException("The topic alias of the publish did not propagate up from the underlying mqtt client's publish request");
            }

            CompareExpectedUserPropertiesWithActual(expected.UserProperties, actual.ApplicationMessage.UserProperties);
        }

        private static void CompareExpectedUserPropertiesWithActual(List<Azure.Iot.Operations.Protocol.Models.MqttUserProperty>? expectedUserProperties, List<MqttUserProperty>? actualUserProperties)
        {
            // Both are null
            if (expectedUserProperties == null && actualUserProperties == null)
            {
                return;
            }

            // Only one is null
            if (expectedUserProperties == null || actualUserProperties == null)
            {
                throw new InvalidOperationException($"The user properties did not propagate down to the underlying mqtt client's request");
            }

            if (expectedUserProperties.Count != actualUserProperties.Count)
            {
                throw new InvalidOperationException($"The user properties did not propagate down to the underlying mqtt client's request");
            }

            foreach (Azure.Iot.Operations.Protocol.Models.MqttUserProperty expectedUserProperty in expectedUserProperties)
            {
                bool matchFound = false;
                foreach (MqttUserProperty actualUserProperty in actualUserProperties)
                {
                    if (string.Equals(expectedUserProperty.Name, actualUserProperty.Name)
                        && string.Equals(expectedUserProperty.Value, actualUserProperty.Value))
                    {
                        matchFound = true;
                        break;
                    }
                }

                if (!matchFound)
                {
                    throw new InvalidOperationException($"The user properties did not propagate down to the underlying mqtt client's request");
                }
            }
        }

        private static void CompareExpectedUserPropertiesWithActual(List<MqttUserProperty>? expectedUserProperties, List<Azure.Iot.Operations.Protocol.Models.MqttUserProperty>? actualUserProperties)
        {
            // Both are null
            if (expectedUserProperties == null && actualUserProperties == null)
            {
                return;
            }

            // Only one is null
            if (expectedUserProperties == null || actualUserProperties == null)
            {
                throw new InvalidOperationException($"The user properties did not propagate down to the underlying mqtt client's request");
            }

            if (expectedUserProperties.Count != actualUserProperties.Count)
            {
                throw new InvalidOperationException($"The user properties did not propagate down to the underlying mqtt client's request");
            }

            foreach (MqttUserProperty expectedUserProperty in expectedUserProperties)
            {
                bool matchFound = false;
                foreach (Azure.Iot.Operations.Protocol.Models.MqttUserProperty actualUserProperty in actualUserProperties)
                {
                    if (string.Equals(expectedUserProperty.Name, actualUserProperty.Name)
                        && string.Equals(expectedUserProperty.Value, actualUserProperty.Value))
                    {
                        matchFound = true;
                        break;
                    }
                }

                if (!matchFound)
                {
                    throw new InvalidOperationException($"The user properties did not propagate down to the underlying mqtt client's request");
                }
            }
        }

        public Task SendEnhancedAuthenticationExchangeDataAsync(MqttEnhancedAuthenticationExchangeData data, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
