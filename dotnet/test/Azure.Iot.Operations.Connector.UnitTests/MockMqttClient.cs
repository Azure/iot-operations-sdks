// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Connector.UnitTests
{
    public class MockMqttClient : IMqttClient
    {
        private string _clientId;
        private readonly MqttProtocolVersion _protocolVersion;
        private bool _isConnected;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public MockMqttClient(string? clientId = null, MqttProtocolVersion protocolVersion = MqttProtocolVersion.V500)
        {
            _clientId = clientId ?? Guid.NewGuid().ToString();
            _protocolVersion = protocolVersion;
        }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        public bool IsConnected => _isConnected;

        public MqttClientOptions Options { get; set; }

        public string? ClientId => _clientId;

        public MqttProtocolVersion ProtocolVersion => _protocolVersion;

        public List<MqttApplicationMessageReceivedEventArgs> AcknowledgedMessages = new();

        public event Func<MqttClientConnectedEventArgs, Task>? ConnectedAsync;
        public event Func<MqttApplicationMessageReceivedEventArgs, Task>? ApplicationMessageReceivedAsync;
        public event Func<MqttClientDisconnectedEventArgs, Task>? DisconnectedAsync;
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
                packetId,
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
                connectResult = new MqttClientConnectResult()
                {
                    ResultCode = MqttClientConnectResultCode.Success,
                    IsSessionPresent = !options.CleanSession,
                };
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

        public Task SendExtendedAuthenticationExchangeDataAsync(MqttExtendedAuthenticationExchangeData data, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
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
                    results.Add(new MqttClientSubscribeResultItem(filter, MqttClientSubscribeReasonCode.GrantedQoS0));
                }
                else if (filter.QualityOfServiceLevel == MqttQualityOfServiceLevel.AtLeastOnce)
                {
                    results.Add(new MqttClientSubscribeResultItem(filter, MqttClientSubscribeReasonCode.GrantedQoS1));
                }
                else
                {
                    results.Add(new MqttClientSubscribeResultItem(filter, MqttClientSubscribeReasonCode.GrantedQoS2));
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
                results.Add(new MqttClientUnsubscribeResultItem(topic, MqttClientUnsubscribeReasonCode.Success));
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

        public Task<MqttClientConnectResult> ConnectAsync(MqttConnectionSettings settings, CancellationToken cancellationToken = default)
        {
            return ConnectAsync(new MqttClientOptions(settings), cancellationToken);
        }

        public Task ReconnectAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public ValueTask DisposeAsync(bool disposing)
        {
            // nothing to dispose
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            // nothing to dispose
            return ValueTask.CompletedTask;
        }
    }
}
