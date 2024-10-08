# Telemetry API

High level API

## Telemetry Sender

* Allows to specify the QoS of the message
* Allows to include metadata as msg properties

```csharp
[ModelId("dtmi:sample:telemetry;1")]
[TelemetryTopicPattern("topic/{withTokens}")]
public abstract class TelemetrySender<T>
    where T : class
{
    public TelemetrySender(
                IMqttSessionClient mqttClient, 
                IPayloadSerializer serializer,
                string? telemetryName);

    public CloudEventsMetadata? CloudEventsMetadata { set; }

    public async Task SendTelemetryAsync(
        T telemetry, 
        OutgoingTelemetryMetadata metadata, 
        MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, 
        TimeSpan? messageExpiryInterval = null, 
        CancellationToken cancellationToken = default)
}
```

## Telemetry Receiver

* Abstract the MQTT Message into a `TelemetryMessage`
* Includes metadata
* Allows to ACK the message

```csharp
\

[ModelId("dtmi:sample:telemetry;1")]
[TelemetryTopicPattern("topic/{withTokens}")]
public abstract class TelemetryReceiver<T>
    where T : class
{
    public Func<T, Task<bool>>? OnTelemetryReceived { get; set; }

    public TelemetryReceiver(
                    IMqttClient mqttClient, 
                    IPayloadSerializer serializer, 
                    string? telemetryName);

    public async Task StartAsync();

    public async Task StopAsync();
}
```
