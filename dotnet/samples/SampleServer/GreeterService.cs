// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Mqtt.Session;
using TestEnvoys.Greeter;

namespace SampleServer;

public class GreeterService(MqttSessionClient mqttClient) : GreeterEnvoy.Service(mqttClient)
{
    public override Task<ExtendedResponse<GreeterEnvoy.HelloResponse>> SayHello(ExtendedRequest<GreeterEnvoy.HelloRequest> request, CancellationToken cancellationToken)
    {
        Console.WriteLine($"--> Executing Greeter.SayHello with id {request.RequestMetadata.CorrelationId} for {request.RequestMetadata.InvokerClientId}");
        Console.WriteLine($"--> Executed Greeter.SayHello with id {request.RequestMetadata.CorrelationId} for {request.RequestMetadata.InvokerClientId}");
        return Task.FromResult(new ExtendedResponse<GreeterEnvoy.HelloResponse>
        {
            Response = new GreeterEnvoy.HelloResponse
            {
                Message = $"Hello {request.Request.Name}"
            }
        });
    }

    public override async Task<ExtendedResponse<GreeterEnvoy.HelloResponse>> SayHelloWithDelayAsync(ExtendedRequest<GreeterEnvoy.HelloWithDelayRequest> request, CancellationToken cancellationToken)
    {
        Console.WriteLine($"--> Executing Greeter.SayHelloWithDelay with id {request.RequestMetadata.CorrelationId} for {request.RequestMetadata.InvokerClientId}");
        if (request.Request.Delay == TimeSpan.Zero)
        {
            throw new ApplicationException("Delay cannot be Zero");
        }
        await Task.Delay(request.Request.Delay, cancellationToken);
        Console.WriteLine($"--> Executed Greeter.SayHelloWithDelay with id {request.RequestMetadata.CorrelationId} for {request.RequestMetadata.InvokerClientId}");
        return new ExtendedResponse<GreeterEnvoy.HelloResponse>
        {
            Response = new GreeterEnvoy.HelloResponse
            {
                Message = $"Hello {request.Request.Name} after {request.Request.Delay}"
            }
        };
    }
}
