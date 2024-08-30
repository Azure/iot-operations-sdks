using MQTTnet.Client;
using TestEnvoys.dtmi_com_example_Counter__1;
using Azure.Iot.Operations.Protocol.RPC;

namespace Azure.Iot.Operations.Protocol.IntegrationTests;

public class CounterService : Counter.Service
{
    int counter = 0;

    public CounterService(IMqttPubSubClient mqttClient) : base(mqttClient) 
    {
        ReadCounterCommandExecutor.ExecutionTimeout = TimeSpan.FromSeconds(30);
        IncrementCommandExecutor.ExecutionTimeout = TimeSpan.FromSeconds(30);
        ResetCommandExecutor.ExecutionTimeout = TimeSpan.FromSeconds(30);
    }

    public override Task<ExtendedResponse<IncrementCommandResponse>> IncrementAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        Console.WriteLine($"--> Executing Counter.Increment with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        Interlocked.Increment(ref counter);
        Console.WriteLine($"--> Executed Counter.Increment with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        return Task.FromResult(new ExtendedResponse<IncrementCommandResponse>
        {
            Response = new IncrementCommandResponse { CounterResponse = counter }
        });
    }

    public override Task<ExtendedResponse<ReadCounterCommandResponse>> ReadCounterAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        Console.WriteLine($"--> Executing Counter.ReadCounter with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        var curValue = counter;
        Console.WriteLine($"--> Executed Counter.ReadCounter with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        return Task.FromResult(new ExtendedResponse<ReadCounterCommandResponse>
        {
            Response = new ReadCounterCommandResponse { CounterResponse = curValue }
        });
    }

    public override Task<CommandResponseMetadata?> ResetAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        Console.WriteLine($"--> Executing Counter.Reset with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        counter = 0;
        Console.WriteLine($"--> Executed Counter.Reset with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        return Task.FromResult(new CommandResponseMetadata())!;
    }
}
