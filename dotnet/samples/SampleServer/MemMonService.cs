// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Telemetry;
using TestEnvoys.Memmon;
using Azure.Iot.Operations.Protocol;

namespace SampleServer;
public class MemMonService : Memmon.Service
{
    private readonly Timer _telemetryTimer;
    private bool _enabled = false;
    private int _interval = 5000;

    private readonly CloudEvent _ceMemStats = new (new Uri("aio://test")) { DataSchema = "123" };
    private readonly CloudEvent _ceWorkingSet = new (new Uri("aio://test")) { DataSchema = "234" };
    private readonly CloudEvent _ceManagedMemory = new (new Uri("aio://test")) { DataSchema = "345" };

    public MemMonService(ApplicationContext applicationContext, MqttSessionClient mqttClient) : base(applicationContext, mqttClient)
    {
        _telemetryTimer = new Timer(SendTelemetryTimer, null, 0, 5000);
    }

    private void SendTelemetryTimer(object? state)
    {
        Task.Run(async () =>
        {
            if (_enabled)
            {
                await SendMemStats();
                await SendTelemetryWorkingSet();
                await SendTelemetryWorkingSet();
            }
        });
    }

    public Task SendTelemetryWorkingSet() =>
        SendTelemetryAsync(
            new WorkingSetTelemetry() { WorkingSet = Environment.WorkingSet},
            new OutgoingTelemetryMetadata() { CloudEvent = _ceWorkingSet });

    public Task SendTelemetryManagedMemory() =>
        SendTelemetryAsync(
            new ManagedMemoryTelemetry { ManagedMemory = GC.GetGCMemoryInfo().TotalCommittedBytes },
            new OutgoingTelemetryMetadata() { CloudEvent = _ceManagedMemory } );

    public Task SendMemStats() =>
        SendTelemetryAsync(
            new MemoryStatsTelemetry
            {
                MemoryStats = new MemoryStatsSchema
                {
                    ManagedMemory = GC.GetGCMemoryInfo().TotalCommittedBytes,
                    WorkingSet = Environment.WorkingSet
                }
            },
            new OutgoingTelemetryMetadata() { CloudEvent = _ceMemStats });

    public override Task<ExtendedResponse<GetRuntimeStatsResponsePayload>> GetRuntimeStatsAsync(GetRuntimeStatsRequestPayload request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        return Task.FromResult(new ExtendedResponse<GetRuntimeStatsResponsePayload>
        {
            Response = new GetRuntimeStatsResponsePayload
            {
                DiagnosticResults = new Dictionary<string, string>
                {
                    { ".NETversion", Environment.Version.ToString() },
                    { "Is64Bit", Environment.Is64BitProcess.ToString() },
                    { "ProcessorCount", Environment.ProcessorCount.ToString() },
                    { "WorkingSet", Environment.WorkingSet.ToString() },
                    { "ManagedMemory", GC.GetGCMemoryInfo().TotalCommittedBytes.ToString() },
                    { "TotalMemory", GC.GetTotalMemory(false).ToString() },
                    { "interval", _interval.ToString() },
                    { "enabled", _enabled.ToString() }
                }
            }
        });
    }

    public override Task<CommandResponseMetadata?> StartTelemetryAsync(StartTelemetryRequestPayload request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        Console.WriteLine("Starting Memmon.Telemetry");
        _enabled = true;
        _interval = request.Interval;
        _telemetryTimer.Change(0, _interval * 1000);
        return Task.FromResult(new CommandResponseMetadata())!;
    }

    public override Task<CommandResponseMetadata?> StopTelemetryAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        Console.WriteLine("Stopping Memmon.Telemetry");
        _enabled = false;
        _telemetryTimer.Change(Timeout.Infinite, 0);
        return Task.FromResult(new CommandResponseMetadata())!;
    }
}
