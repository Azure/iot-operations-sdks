// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Mqtt.Converters;

namespace Azure.Iot.Operations.Protocol.MetlTests;

public sealed class CompositeMqttClient : IAsyncDisposable, IMqttPubSubClient
{
    private readonly MqttClientOptions _connectOptions;
    private readonly MqttClientDisconnectOptions _disconnectOptions;

    private readonly MQTTnet.IMqttClient _mqttClient;
    private readonly MqttSessionClient? _sessionClient;

    public CompositeMqttClient(MQTTnet.IMqttClient mqttClient, bool includeSessionClient, string clientId)
    {
        _connectOptions = new MqttClientOptions(new MqttClientTcpOptions("localhost", 1883)) { SessionExpiryInterval = 120, ClientId = clientId };
        _disconnectOptions = new MqttClientDisconnectOptions();

        this._mqttClient = mqttClient;

        if (includeSessionClient)
        {
            this._sessionClient = new MqttSessionClient(mqttClient);
        }
    }

    public event Func<MqttApplicationMessageReceivedEventArgs, Task>? ApplicationMessageReceivedAsync
    {
        add
        {
            if (_sessionClient != null)
            {
                _sessionClient.ApplicationMessageReceivedAsync += value;
            }
            else
            {
                _mqttClient.ApplicationMessageReceivedAsync += MqttNetConverter.FromGeneric(value!);
            }
        }

        remove
        {
            if (_sessionClient != null)
            {
                _sessionClient.ApplicationMessageReceivedAsync -= value;
            }
            else
            {
                _mqttClient.ApplicationMessageReceivedAsync -= MqttNetConverter.FromGeneric(value!);
            }
        }
    }

    public async Task<MqttClientPublishResult> PublishAsync(MqttApplicationMessage applicationMessage, CancellationToken cancellationToken = default) => _sessionClient != null ?
        await _sessionClient.PublishAsync(applicationMessage, cancellationToken) :
        MqttNetConverter.ToGeneric(await _mqttClient.PublishAsync(MqttNetConverter.FromGeneric(applicationMessage), cancellationToken));

    public async Task<MqttClientSubscribeResult> SubscribeAsync(MqttClientSubscribeOptions options, CancellationToken cancellationToken = default) => _sessionClient != null ?
        await _sessionClient.SubscribeAsync(options, cancellationToken) :
        MqttNetConverter.ToGeneric(await _mqttClient.SubscribeAsync(MqttNetConverter.FromGeneric(options), cancellationToken));

    public async Task<MqttClientUnsubscribeResult> UnsubscribeAsync(MqttClientUnsubscribeOptions options, CancellationToken cancellationToken = default) => _sessionClient != null ?
        await _sessionClient.UnsubscribeAsync(options, cancellationToken) :
        MqttNetConverter.ToGeneric(await _mqttClient.UnsubscribeAsync(MqttNetConverter.FromGeneric(options), cancellationToken));

    public string ClientId { get => _sessionClient != null ? _sessionClient.ClientId! : _mqttClient.Options?.ClientId ?? string.Empty; }

    public MqttProtocolVersion ProtocolVersion { get => _sessionClient != null ? _sessionClient.ProtocolVersion : (MqttProtocolVersion)((int) (_mqttClient.Options?.ProtocolVersion ?? MQTTnet.Formatter.MqttProtocolVersion.Unknown)); }

    public async Task<MqttClientConnectResult> ConnectAsync(CancellationToken cancellationToken = default) => _sessionClient != null ?
        await _sessionClient.ConnectAsync(_connectOptions, cancellationToken) :
        MqttNetConverter.ToGeneric(await _mqttClient.ConnectAsync(MqttNetConverter.FromGeneric(_connectOptions, _mqttClient), cancellationToken))!;

    public Task DisconnectAsync(CancellationToken cancellationToken = default) => _sessionClient != null ?
        _sessionClient.DisconnectAsync(_disconnectOptions, cancellationToken) :
        _mqttClient.DisconnectAsync(MqttNetConverter.FromGeneric(_disconnectOptions), cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (_sessionClient != null)
        {
            await _sessionClient.DisconnectAsync();
            await _sessionClient.DisposeAsync();
        }
        else
        {
            await _mqttClient.DisconnectAsync(new MQTTnet.MqttClientDisconnectOptions());
            _mqttClient.Dispose();
        }
    }

    public ValueTask DisposeAsync(bool disposing)
    {
        return DisposeAsync();
    }
}