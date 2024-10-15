using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Protocol.Telemetry;
using TestEnvoys.dtmi_akri_samples_memmon__1;

namespace Azure.Iot.Operations.Protocol.IntegrationTests;

public class MemMonService : Memmon.Service
{
    bool enabled = false;
    int interval = 5000;

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
        return Task.FromResult(new CommandResponseMetadata())!;
    }

    public override Task<CommandResponseMetadata?> StopTelemetryAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        Console.WriteLine("Stopping Memmon.Telemetry");
        enabled = false;
        return Task.FromResult(new CommandResponseMetadata())!;
    }
}
