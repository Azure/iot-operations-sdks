// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Mqtt.Session;
using TestEnvoys.Counter;
using Azure.Iot.Operations.Protocol.Telemetry;

namespace CounterClient;

public class CounterClient(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, ILogger<CounterClient> logger) : Counter.Client(applicationContext, mqttClient)
{
    private static long telemetryCount = 0;

    public static Func<IServiceProvider, CounterClient> Factory = service => new CounterClient(service.GetRequiredService<ApplicationContext>(), service.GetService<MqttSessionClient>()!, service.GetService<ILogger<CounterClient>>()!);

    public override Task ReceiveTelemetry(string senderId, TelemetryCollection telemetry, IncomingTelemetryMetadata metadata)
    {
        // Log or process telemetry data
        logger.LogInformation($"Telemetry received from {senderId}: CounterValue={telemetry.CounterValue}");
        Interlocked.Increment(ref telemetryCount);
        return Task.CompletedTask;
    }

    public long GetTelemetryCount()
    {
        return Interlocked.Read(ref telemetryCount);
    }

}
