// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Mqtt.Session;
using System.Diagnostics;
using TestEnvoys.Math;

namespace SampleServer;

public class MathService(MqttSessionClient mqttClient) : TestEnvoys.Math.Math.Service(mqttClient)
{
    public override Task<ExtendedResponse<FibResponsePayload>> FibAsync(FibRequestPayload request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        Console.WriteLine($"--> Executing Math.Fib({request.FibRequest.Number}) with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");

        static int FibLoop(int n)
        {
            if (n <= 1) return n;
            return FibLoop(n - 1) + FibLoop(n - 2);
        }

        int result = FibLoop(request.FibRequest.Number);
        Console.WriteLine($"--> Executed Math.Fib({request.FibRequest.Number}) with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        return Task.FromResult(new ExtendedResponse<FibResponsePayload>
        {
            Response = new FibResponsePayload
            {
                FibResponse = new FibResponseSchema
                {
                    FibResult = result
                }
            }
        });
    }

    public override Task<ExtendedResponse<GetRandomResponsePayload>> GetRandomAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        Console.WriteLine($"--> Executing Math.GetRandom with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        int result = new Random().Next(50);
        Console.WriteLine($"--> Executed Math.GetRandom with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        return Task.FromResult(new ExtendedResponse<GetRandomResponsePayload>
        {
            Response = new GetRandomResponsePayload()
            {
                GetRandomResponse = result
            }
        });
    }

    public override Task<ExtendedResponse<IsPrimeResponsePayload>> IsPrimeAsync(IsPrimeRequestPayload request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        Console.WriteLine($"--> Executing IsPrime({request.IsPrimeRequest.Number}) with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        Stopwatch clock = Stopwatch.StartNew();

        IsPrimeResponsePayload response = new()
        {
            IsPrimeResponse = new IsPrimeResponseSchema
            {
                Number = request.IsPrimeRequest.Number,
                ExecutorId = mqttClient.ClientId,
                InvokerId = request.IsPrimeRequest.InvokerId
            }
        };


        int numOps = 0;
        bool FindPrime(int number)
        {
            response.IsPrimeResponse.ThreadId = Environment.CurrentManagedThreadId;
            bool result = true;
            var combinations = from n1 in Enumerable.Range(2, number / 2)
                               from n2 in Enumerable.Range(2, n1)
                               select new { n1, n2 };
            foreach (var op in combinations)
            {
                numOps++;
                //res.Ops.Add($"{op.n1} x {op.n2} => {number}");
                if (op.n1 * op.n2 == number)
                {
                    result = false;
                    break;
                }
            }
            return result;

        }
        bool result = FindPrime(request.IsPrimeRequest.Number);
        clock.Stop();
        //res.ComputeMS = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds() - req.InvokerStartTime;
        //response.IsPrimerResponse.ComputeMS = clock.Elapsed.;
        response.IsPrimeResponse.IsPrime = result;
        Console.WriteLine($"--> Executed IsPrime({request.IsPrimeRequest.Number}) took {numOps} ops. with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        return Task.FromResult(new ExtendedResponse<IsPrimeResponsePayload> { Response = response });
    }
}
