// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using TestEnvoys.Counter;
using Azure.Iot.Operations.Protocol.RPC;
using System.Text.Json;

namespace Azure.Iot.Operations.Protocol.IntegrationTests;

public class CounterService : Counter.Service
{
    private int _counter = 0;

    public ExtendedCloudEvent? ReceivedCloudEvent { get; private set; }
    public CloudEvent? PublishedResponseCloudEvent { get; private set; }

    public const string NegativeValueArgumentErrorCode = "NegativeValue";

    public CounterService(ApplicationContext applicationContext, IMqttPubSubClient mqttClient) : base(applicationContext, mqttClient) 
    {
        ReadCounterActionExecutor.ExecutionTimeout = TimeSpan.FromSeconds(30);
        IncrementActionExecutor.ExecutionTimeout = TimeSpan.FromSeconds(30);
        ResetActionExecutor.ExecutionTimeout = TimeSpan.FromSeconds(30);
    }

    public override Task<ExtendedResponse<IncrementOutputArguments>> IncrementAsync(IncrementInputArguments request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        if (request.IncrementValue < 0)
        {
            var response =
                new ExtendedResponse<IncrementOutputArguments>()
                {
                    Response = new IncrementOutputArguments { CounterResponse = _counter },
                }.WithApplicationError(
                    NegativeValueArgumentErrorCode,
                    JsonSerializer.Serialize(new CounterServiceApplicationError() { InvalidRequestArgumentValue = request.IncrementValue }));

            return Task.FromResult(response);
        }

        Console.WriteLine($"--> Executing Counter.Increment with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        Interlocked.Increment(ref _counter);
        Console.WriteLine($"--> Executed Counter.Increment with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        return Task.FromResult(new ExtendedResponse<IncrementOutputArguments>
        {
            Response = new IncrementOutputArguments { CounterResponse = _counter }
        });
    }

    public override Task<ExtendedResponse<ReadCounterOutputArguments>> ReadCounterAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        CommandResponseMetadata responseMetadata = new();

        if (requestMetadata.ExtendedCloudEvent != null)
        {
            ReceivedCloudEvent = requestMetadata.ExtendedCloudEvent;

            PublishedResponseCloudEvent = new(new Uri("https://www.contoso.com"), "someRpcResponse.type")
            {
                DataSchema = "https://www.contoso.com",
                Id = Guid.NewGuid().ToString(),

                //echo back the time and subject for testing purposes
                Subject = ReceivedCloudEvent.Subject,
                Time = ReceivedCloudEvent.Time
            };

            responseMetadata.CloudEvent = PublishedResponseCloudEvent;
        }

        Console.WriteLine($"--> Executing Counter.ReadCounter with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        var curValue = _counter;
        Console.WriteLine($"--> Executed Counter.ReadCounter with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        return Task.FromResult(new ExtendedResponse<ReadCounterOutputArguments>
        {
            Response = new ReadCounterOutputArguments { CounterResponse = curValue },
            ResponseMetadata = responseMetadata
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
