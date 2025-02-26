// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Mqtt.Session;
using TestEnvoys.Counter;
using Azure.Iot.Operations.Protocol.Telemetry;
using Azure.Iot.Operations.Protocol;

namespace CounterServer;

public class CounterService(ApplicationContext applicationContext, MqttSessionClient mqttClient, ILogger<CounterService> logger) : Counter.Service(applicationContext, mqttClient)
{
    int counter = 0;

    public async override Task<ExtendedResponse<IncrementResponsePayload>> IncrementAsync(IncrementRequestPayload request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        logger.LogInformation($"--> Executing Counter.Increment with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        // Use the increment value from the request
        Interlocked.Add(ref counter, request.IncrementValue);
        logger.LogInformation($"--> Executed Counter.Increment with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");

        // Prepare telemetry payload with the updated counter value
        var telemetryPayload = new TelemetryCollection
        {
            CounterValue = counter
        };

        // Send telemetry using the telemetry sender
        var metadata = new OutgoingTelemetryMetadata();
        await this.SendTelemetryAsync(telemetryPayload, metadata, cancellationToken: cancellationToken);

        return new ExtendedResponse<IncrementResponsePayload>
        {
            Response = new IncrementResponsePayload { CounterResponse = counter }
        };
    }

    public override Task<ExtendedResponse<ReadCounterResponsePayload>> ReadCounterAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        logger.LogInformation($"--> Executing Counter.ReadCounter with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        var curValue = counter;
        logger.LogInformation($"--> Executed Counter.ReadCounter with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        return Task.FromResult(new ExtendedResponse<ReadCounterResponsePayload>
        {
            Response = new ReadCounterResponsePayload { CounterResponse = curValue }
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
