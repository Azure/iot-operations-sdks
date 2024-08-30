using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Mqtt.Session;
using TestEnvoys.dtmi_com_example_Counter__1;

namespace CounterServer;

public class CounterService(MqttSessionClient mqttClient, ILogger<CounterService> logger) : Counter.Service(mqttClient)
{
    int counter = 0;

    public override Task<ExtendedResponse<IncrementCommandResponse>> IncrementAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        logger.LogInformation($"--> Executing Counter.Increment with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        Interlocked.Increment(ref counter);
        logger.LogInformation($"--> Executed Counter.Increment with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        return Task.FromResult(new ExtendedResponse<IncrementCommandResponse>
        {
            Response = new IncrementCommandResponse { CounterResponse = counter }
        });
    }

    public override Task<ExtendedResponse<ReadCounterCommandResponse>> ReadCounterAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        logger.LogInformation($"--> Executing Counter.ReadCounter with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        var curValue = counter;
        logger.LogInformation($"--> Executed Counter.ReadCounter with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        return Task.FromResult(new ExtendedResponse<ReadCounterCommandResponse>
        {
            Response = new ReadCounterCommandResponse { CounterResponse = curValue }
        });
    }

    public override Task<CommandResponseMetadata?> ResetAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        logger.LogInformation($"--> Executing Counter.Reset with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        counter = 0;
        logger.LogInformation($"--> Executed Counter.Reset with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        return Task.FromResult(new CommandResponseMetadata())!;
    }
}
