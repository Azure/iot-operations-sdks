// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Mqtt.Session;
using TestThing.Counter;
using Azure.Iot.Operations.Protocol;
using System.Text.Json.Nodes;
using System.Text.Json;

namespace SampleServer;

public class CounterService : Counter.Service
{
    private int _counter = 0;

    public CounterService(ApplicationContext applicationContext, MqttSessionClient mqttClient) : base(applicationContext, mqttClient) { }

    public override Task<ExtendedResponse<IncrementOutputArguments>> IncrementAsync(IncrementInputArguments request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        Console.WriteLine($"--> Executing Counter.Increment with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");

        if (request.IncrementValue < 0)
        {
            var response =
                new ExtendedResponse<IncrementOutputArguments>()
                {
                    Response = new IncrementOutputArguments { CounterResponse = _counter },
                }
                .WithApplicationError(
                    "negativeValue",
                    JsonSerializer.Serialize(new CounterServiceApplicationError() { InvalidRequestArgumentValue = request.IncrementValue }));

            return Task.FromResult(response);
        }

        Interlocked.Add(ref _counter, request.IncrementValue);
        Console.WriteLine($"--> Executed Counter.Increment with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        return Task.FromResult(new ExtendedResponse<IncrementOutputArguments>
        {
            Response = new IncrementOutputArguments { CounterResponse = _counter }
        });
    }

    public override Task<ExtendedResponse<ReadCounterOutputArguments>> ReadCounterAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        Console.WriteLine($"--> Executing Counter.ReadCounter with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        var curValue = _counter;
        Console.WriteLine($"--> Executed Counter.ReadCounter with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        return Task.FromResult(new ExtendedResponse<ReadCounterOutputArguments>
        {
            Response = new ReadCounterOutputArguments { CounterResponse = curValue }
        });
    }

    public override Task<CommandResponseMetadata?> ResetAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        Console.WriteLine($"--> Executing Counter.Reset with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        _counter = 0;
        Console.WriteLine($"--> Executed Counter.Reset with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        return Task.FromResult(new CommandResponseMetadata())!;
    }
}
