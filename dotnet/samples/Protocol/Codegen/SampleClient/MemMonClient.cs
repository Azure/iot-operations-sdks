﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Telemetry;
using TestEnvoys.Memmon;

namespace SampleClient;

internal class MemMonClient(ApplicationContext applicationContext, MqttSessionClient mqttClient, ILogger<MemMonClient> logger) : Memmon.Client(applicationContext, mqttClient)
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
