// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using IMqttClient = MQTTnet.IMqttClient;

namespace Azure.Iot.Operations.Mqtt;

public class ExtendedPubSubMqttClient(IMqttClient mqttNetClient, OrderedAckMqttClientOptions? clientOptions = null)
    : OrderedAckMqttClient(mqttNetClient, clientOptions), IExtendedPubSubMqttClient
{
    private MqttClientConnectResult? _connectResult;

    public override async Task<MqttClientConnectResult> ConnectAsync(MqttClientOptions options, CancellationToken cancellationToken = default)
    {
        var connectResult = await base.ConnectAsync(options, cancellationToken);
        _connectResult = connectResult;
        return connectResult;
    }

    public override async Task<MqttClientConnectResult> ConnectAsync(MqttConnectionSettings settings, CancellationToken cancellationToken = default)
    {
        var connectResult = await base.ConnectAsync(settings, cancellationToken);
        _connectResult = connectResult;
        return connectResult;
    }

    public MqttClientConnectResult? GetConnectResult()
    {
        return _connectResult;
    }
}
