// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.RPC;
using TestEnvoys.Memmon;

namespace Azure.Iot.Operations.Protocol.IntegrationTests;

public class MemMonService : Memmon.Service
{
    bool _enabled = false;
    int _interval = 5000;

    public MemMonService(IMqttPubSubClient mqttClient) : base(mqttClient)
    {
        StartTelemetryCommandExecutor.ExecutionTimeout = TimeSpan.FromSeconds(30);
        StopTelemetryCommandExecutor.ExecutionTimeout = TimeSpan.FromSeconds(30);
        GetRuntimeStatsCommandExecutor.ExecutionTimeout = TimeSpan.FromSeconds(30);
    }

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
        return Task.FromResult(new CommandResponseMetadata())!;
    }

    public override Task<CommandResponseMetadata?> StopTelemetryAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        Console.WriteLine("Stopping Memmon.Telemetry");
        _enabled = false;
        return Task.FromResult(new CommandResponseMetadata())!;
    }
}
