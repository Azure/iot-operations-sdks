// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.RPC;
using System.Diagnostics;
using TestEnvoys.dtmi_rpc_samples_math__1;

namespace Azure.Iot.Operations.Protocol.IntegrationTests;

public class MathService : TestEnvoys.dtmi_rpc_samples_math__1.Math.Service
{
    IMqttPubSubClient _mqttClient;
    public MathService(IMqttPubSubClient mqttClient) : base(mqttClient)
    {
        _mqttClient = mqttClient;
        FibCommandExecutor.ExecutionTimeout = TimeSpan.FromSeconds(30);
        IsPrimeCommandExecutor.ExecutionTimeout = TimeSpan.FromSeconds(30);
        GetRandomCommandExecutor.ExecutionTimeout = TimeSpan.FromSeconds(30);     
    }

    public override Task<ExtendedResponse<FibResponsePayload>> FibAsync(FibRequestPayload request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        System.Console.WriteLine($"--> Executing Math.Fib({request.FibRequest.Number}) with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        int FibLoop(int n)
        {
            if (n <= 1) return n;
            return FibLoop(n - 1) + FibLoop(n - 2);
        }

        var result = FibLoop(request.FibRequest.Number);
        System.Console.WriteLine($"--> Executed Math.Fib({request.FibRequest.Number}) with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        return Task.FromResult(new ExtendedResponse<FibResponsePayload>
        {
            Response = new FibResponsePayload
            {
                FibResponse = new Object_Fib_Response
                {
                    FibResult = result
                }
            }
        });
    }

    public override Task<ExtendedResponse<GetRandomResponsePayload>> GetRandomAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        int randomSeed = requestMetadata.CorrelationId.GetHashCode();
        System.Console.WriteLine($"--> Executing Math.GetRandom with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
        var result = new Random(randomSeed).Next(50);
        System.Console.WriteLine($"--> Executed Math.GetRandom with id {requestMetadata.CorrelationId} for {requestMetadata.InvokerClientId}");
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
            IsPrimeResponse = new Object_IsPrime_Response
            {
                Number = request.IsPrimeRequest.Number,
                ExecutorId = _mqttClient.ClientId,
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
        return Task.FromResult(new ExtendedResponse<IsPrimeResponsePayload> { Response = response});
    }
}
