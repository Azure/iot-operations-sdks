﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using TestEnvoys.Counter;
using Azure.Iot.Operations.Protocol.RPC;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Azure.Iot.Operations.Protocol.IntegrationTests;

public class CounterService : Counter.Service
{
    private int _counter = 0;

    public const string NegativeValueArgumentErrorCode = "NegativeValue";

    public CounterService(ApplicationContext applicationContext, IMqttPubSubClient mqttClient) : base(applicationContext, mqttClient) 
    {
        ReadCounterCommandExecutor.ExecutionTimeout = TimeSpan.FromSeconds(30);
        IncrementCommandExecutor.ExecutionTimeout = TimeSpan.FromSeconds(30);
        ResetCommandExecutor.ExecutionTimeout = TimeSpan.FromSeconds(30);
    }

    public override Task<ExtendedResponse<IncrementResponsePayload>> IncrementAsync(IncrementRequestPayload request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        if (request.IncrementValue < 0)
        {
            var response =
                new ExtendedResponse<IncrementResponsePayload>()
                {
                    Response = new IncrementResponsePayload { CounterResponse = _counter },
                }.WithApplicationError(
                    NegativeValueArgumentErrorCode,
                    JsonSerializer.Serialize(new CounterServiceApplicationError() { InvalidRequestArgumentValue = request.IncrementValue }));

            return Task.FromResult(response);
        }

        Console.WriteLine($"--> Executing Counter.Increment with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        Interlocked.Increment(ref _counter);
        Console.WriteLine($"--> Executed Counter.Increment with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        return Task.FromResult(new ExtendedResponse<IncrementResponsePayload>
        {
            Response = new IncrementResponsePayload { CounterResponse = _counter }
        });
    }

    public override Task<ExtendedResponse<ReadCounterResponsePayload>> ReadCounterAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        Console.WriteLine($"--> Executing Counter.ReadCounter with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        var curValue = _counter;
        Console.WriteLine($"--> Executed Counter.ReadCounter with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        return Task.FromResult(new ExtendedResponse<ReadCounterResponsePayload>
        {
            Response = new ReadCounterResponsePayload { CounterResponse = curValue }
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
