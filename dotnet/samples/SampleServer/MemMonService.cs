// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Telemetry;
using TestEnvoys.dtmi_akri_samples_memmon__1;

namespace SampleServer;
public class MemMonService : Memmon.Service
{
    private readonly Timer _telemetryTimer;
    private bool enabled = false;
    private int interval = 5000;

    private readonly CloudEvent _ceMemStats = new (new Uri("aio://test")) { DataSchema = "123" };
    private readonly CloudEvent _ceWorkingSet = new (new Uri("aio://test")) { DataSchema = "234" };
    private readonly CloudEvent _ceManagedMemory = new (new Uri("aio://test")) { DataSchema = "345" };

    public MemMonService(MqttSessionClient mqttClient) : base(mqttClient)
    {
        _telemetryTimer = new Timer(SendTelemetryTimer, null, 0, 5000);
    }

    private void SendTelemetryTimer(object? state)
    {
        Task.Run(async () =>
        {
            if (enabled)
            {
                await SendMemStats();
                await SendTelemetryWorkingSet();
                await SendTelemetryWorkingSet();
            }
        });
    }

    public Task SendTelemetryWorkingSet() =>
        SendTelemetryAsync(
            new WorkingSetTelemetry() { workingSet = Environment.WorkingSet},
            new OutgoingTelemetryMetadata() { CloudEvent = _ceWorkingSet });

    public Task SendTelemetryManagedMemory() =>
        SendTelemetryAsync(
            new ManagedMemoryTelemetry { managedMemory = GC.GetGCMemoryInfo().TotalCommittedBytes },
            new OutgoingTelemetryMetadata() { CloudEvent = _ceManagedMemory } );

    public Task SendMemStats() =>
        SendTelemetryAsync(
            new MemoryStatsTelemetry
            {
                memoryStats = new Object_MemoryStats
                {
                    managedMemory = GC.GetGCMemoryInfo().TotalCommittedBytes,
                    workingSet = Environment.WorkingSet
                }
            }, 
            new OutgoingTelemetryMetadata() { CloudEvent = _ceMemStats });

    public override Task<ExtendedResponse<GetRuntimeStatsResponsePayload>> GetRuntimeStatsAsync(GetRuntimeStatsRequestPayload request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        return Task.FromResult(new ExtendedResponse<GetRuntimeStatsResponsePayload>
        {
            Response = new GetRuntimeStatsResponsePayload
            {
                diagnosticResults = new Dictionary<string, string>
                {
                    { ".NETversion", Environment.Version.ToString() },
                    { "Is64Bit", Environment.Is64BitProcess.ToString() },
                    { "ProcessorCount", Environment.ProcessorCount.ToString() },
                    { "WorkingSet", Environment.WorkingSet.ToString() },
                    { "ManagedMemory", GC.GetGCMemoryInfo().TotalCommittedBytes.ToString() },
                    { "TotalMemory", GC.GetTotalMemory(false).ToString() },
                    { "interval", interval.ToString() },
                    { "enabled", enabled.ToString() }
                }
            }
        });
    }

    public override Task<CommandResponseMetadata?> StartTelemetryAsync(StartTelemetryRequestPayload request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        Console.WriteLine("Starting Memmon.Telemetry");
        enabled = true;
        interval = request.interval;
        _telemetryTimer.Change(0, interval * 1000);
        return Task.FromResult(new CommandResponseMetadata())!;
    }

    public override Task<CommandResponseMetadata?> StopTelemetryAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        Console.WriteLine("Stopping Memmon.Telemetry");
        enabled = false;
        _telemetryTimer.Change(Timeout.Infinite, 0);
        return Task.FromResult(new CommandResponseMetadata())!;
    }
}
