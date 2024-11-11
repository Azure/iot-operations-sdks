using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Mqtt.Converters;

namespace Azure.Iot.Operations.Protocol.MetlTests;

public sealed class CompositeMqttClient : IAsyncDisposable, IMqttPubSubClient
{
    private readonly MqttClientOptions connectOptions;
    private readonly MqttClientDisconnectOptions disconnectOptions;

    private readonly MQTTnet.Client.IMqttClient mqttClient;
    private readonly MqttSessionClient? sessionClient;

    public CompositeMqttClient(MQTTnet.Client.IMqttClient mqttClient, bool includeSessionClient, string clientId)
    {
        connectOptions = new MqttClientOptions(new MqttClientTcpOptions("localhost", 1883)) { SessionExpiryInterval = 120, ClientId = clientId };
        disconnectOptions = new MqttClientDisconnectOptions();

        this.mqttClient = mqttClient;

        if (includeSessionClient)
        {
            this.sessionClient = new MqttSessionClient(mqttClient);
        }
    }

    public event Func<MqttApplicationMessageReceivedEventArgs, Task>? ApplicationMessageReceivedAsync
    {
        add
        {
            if (sessionClient != null)
            {
                sessionClient.ApplicationMessageReceivedAsync += value;
            }
            else
            {
                mqttClient.ApplicationMessageReceivedAsync += MqttNetConverter.FromGeneric(value!);
            }
        }

        remove
        {
            if (sessionClient != null)
            {
                sessionClient.ApplicationMessageReceivedAsync -= value;
            }
            else
            {
                mqttClient.ApplicationMessageReceivedAsync -= MqttNetConverter.FromGeneric(value!);
            }
        }
    }

    public async Task<MqttClientPublishResult> PublishAsync(MqttApplicationMessage applicationMessage, CancellationToken cancellationToken = default) => sessionClient != null ?
        await sessionClient.PublishAsync(applicationMessage, cancellationToken) :
        MqttNetConverter.ToGeneric(await mqttClient.PublishAsync(MqttNetConverter.FromGeneric(applicationMessage), cancellationToken));

    public async Task<MqttClientSubscribeResult> SubscribeAsync(MqttClientSubscribeOptions options, CancellationToken cancellationToken = default) => sessionClient != null ?
        await sessionClient.SubscribeAsync(options, cancellationToken) :
        MqttNetConverter.ToGeneric(await mqttClient.SubscribeAsync(MqttNetConverter.FromGeneric(options), cancellationToken));

    public async Task<MqttClientUnsubscribeResult> UnsubscribeAsync(MqttClientUnsubscribeOptions options, CancellationToken cancellationToken = default) => sessionClient != null ?
        await sessionClient.UnsubscribeAsync(options, cancellationToken) :
        MqttNetConverter.ToGeneric(await mqttClient.UnsubscribeAsync(MqttNetConverter.FromGeneric(options), cancellationToken));

    public string ClientId { get => sessionClient != null ? sessionClient.ClientId! : mqttClient.Options?.ClientId ?? string.Empty; }

    public MqttProtocolVersion ProtocolVersion { get => sessionClient != null ? sessionClient.ProtocolVersion : (MqttProtocolVersion)((int) (mqttClient.Options?.ProtocolVersion ?? MQTTnet.Formatter.MqttProtocolVersion.Unknown)); }

    public async Task<MqttClientConnectResult> ConnectAsync(CancellationToken cancellationToken = default) => sessionClient != null ?
        await sessionClient.ConnectAsync(connectOptions, cancellationToken) :
        MqttNetConverter.ToGeneric(await mqttClient.ConnectAsync(MqttNetConverter.FromGeneric(connectOptions, mqttClient), cancellationToken))!;

    public Task DisconnectAsync(CancellationToken cancellationToken = default) => sessionClient != null ?
        sessionClient.DisconnectAsync(disconnectOptions, cancellationToken) :
        mqttClient.DisconnectAsync(MqttNetConverter.FromGeneric(disconnectOptions), cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (sessionClient != null)
        {
            await sessionClient.DisconnectAsync();
            await sessionClient.DisposeAsync();
        }
        else
        {
            await mqttClient.DisconnectAsync(new MQTTnet.Client.MqttClientDisconnectOptions());
            mqttClient.Dispose();
        }
    }

    public ValueTask DisposeAsync(bool disposing)
    {
        return DisposeAsync();
    }
}