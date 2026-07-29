// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Telemetry;
using Azure.Iot.Operations.Protocol;
using TestThing.Counter;

namespace CounterServer;

public class CounterService(ApplicationContext applicationContext, MqttSessionClient mqttClient, ILogger<CounterService> logger) : Counter.Service(applicationContext, mqttClient)
{
    private int _counter = 0;

    public async override Task<ExtendedResponse<IncrementOutputArguments>> IncrementAsync(IncrementInputArguments request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        logger.LogInformation($"--> Executing Counter.Increment with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        // Use the increment value from the request
        Interlocked.Add(ref _counter, request.IncrementValue);
        logger.LogInformation($"--> Executed Counter.Increment with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");

        // Prepare telemetry payload with the updated counter value
        var telemetryPayload = new EventCollection
        {
            CounterValue = _counter
        };

        // Send telemetry using the telemetry sender
        var metadata = new OutgoingTelemetryMetadata();
        await SendTelemetryAsync(telemetryPayload, metadata, cancellationToken: cancellationToken);

        return new ExtendedResponse<IncrementOutputArguments>
        {
            Response = new IncrementOutputArguments { CounterResponse = _counter }
        };
    }

    public override Task<ExtendedResponse<ReadCounterOutputArguments>> ReadCounterAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        logger.LogInformation($"--> Executing Counter.ReadCounter with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        var curValue = _counter;
        logger.LogInformation($"--> Executed Counter.ReadCounter with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        return Task.FromResult(new ExtendedResponse<ReadCounterOutputArguments>
        {
            Response = new ReadCounterOutputArguments { CounterResponse = curValue }
        });
    }

    public override Task<CommandResponseMetadata?> ResetAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        logger.LogInformation($"--> Executing Counter.Reset with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        _counter = 0;
        logger.LogInformation($"--> Executed Counter.Reset with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        return Task.FromResult(new CommandResponseMetadata())!;
    }
}
