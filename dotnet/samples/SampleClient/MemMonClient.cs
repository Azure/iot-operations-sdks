// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Telemetry;
using TestEnvoys.dtmi_akri_samples_memmon__1;

namespace SampleClient;

internal class MemMonClient(MqttSessionClient mqttClient, ILogger<MemMonClient> logger) : Memmon.Client(mqttClient)
{
    public override Task ReceiveTelemetry(string senderId, WorkingSetTelemetry telemetry, IncomingTelemetryMetadata metadata)
    {
        logger.LogInformation("Rcv WorkingSet Telemetry {v}", telemetry.WorkingSet);
        return Task.CompletedTask;
    }

    public override Task ReceiveTelemetry(string senderId, ManagedMemoryTelemetry telemetry, IncomingTelemetryMetadata metadata)
    {
        logger.LogInformation("Rcv ManagedMemory Telemetry {v}", telemetry.ManagedMemory);
        return Task.CompletedTask;
    }

    public override Task ReceiveTelemetry(string senderId, MemoryStatsTelemetry telemetry, IncomingTelemetryMetadata metadata)
    {
        logger.LogInformation("Rcv MemStats Telemetry {v1} {v2}", telemetry.MemoryStats.WorkingSet, telemetry.MemoryStats.ManagedMemory);

        try
        {
            CloudEvent cloudEvent = metadata.GetCloudEvent();
            logger.LogInformation("Cloud Events Metadata {v1} {v2}", cloudEvent?.Id, cloudEvent?.Time);
        }
        catch (Exception)
        {
            // it wasn't a cloud event, ignore this error
        }

        return Task.CompletedTask;
    }
}
