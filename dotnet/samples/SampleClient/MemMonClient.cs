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
        logger.LogInformation("Rcv WorkingSet Telemetry {v}", telemetry.workingSet);
        return Task.CompletedTask;
    }

    public override Task ReceiveTelemetry(string senderId, ManagedMemoryTelemetry telemetry, IncomingTelemetryMetadata metadata)
    {
        logger.LogInformation("Rcv ManagedMemory Telemetry {v}", telemetry.managedMemory);
        return Task.CompletedTask;
    }

    public override Task ReceiveTelemetry(string senderId, MemoryStatsTelemetry telemetry, IncomingTelemetryMetadata metadata)
    {
        logger.LogInformation("Rcv MemStats Telemetry {v1} {v2}", telemetry.memoryStats.workingSet, telemetry.memoryStats.managedMemory);
        logger.LogInformation("Cloud Events Metadata {v1} {v2}", metadata.CloudEvent?.Id, metadata.CloudEvent?.Time);
        return Task.CompletedTask;
    }
}
