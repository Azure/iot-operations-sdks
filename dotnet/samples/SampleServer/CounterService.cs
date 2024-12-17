// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Mqtt.Session;
using TestEnvoys.dtmi_com_example_Counter__1;

namespace SampleServer;

public class CounterService : Counter.Service
{
    int counter = 0;

    public CounterService(MqttSessionClient mqttClient) : base(mqttClient) { }

    public override Task<ExtendedResponse<IncrementResponsePayload>> IncrementAsync(IncrementRequestPayload request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        Console.WriteLine($"--> Executing Counter.Increment with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        Interlocked.Add(ref counter, request.IncrementValue);
        Console.WriteLine($"--> Executed Counter.Increment with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        return Task.FromResult(new ExtendedResponse<IncrementResponsePayload>
        {
            Response = new IncrementResponsePayload { CounterResponse = counter }
        });
    }

    public override Task<ExtendedResponse<ReadCounterResponsePayload>> ReadCounterAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        Console.WriteLine($"--> Executing Counter.ReadCounter with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        var curValue = counter;
        Console.WriteLine($"--> Executed Counter.ReadCounter with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        return Task.FromResult(new ExtendedResponse<ReadCounterResponsePayload>
        {
            Response = new ReadCounterResponsePayload { CounterResponse = curValue }
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
