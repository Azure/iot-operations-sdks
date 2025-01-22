// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.RPC;
using MQTTnet.Client;
using MQTTnet.Server;
using TestEnvoys.dtmi_com_example_Counter__1;
using TestEnvoys.dtmi_rpc_samples_math__1;
using TestEnvoys.Greeter;
using TestEnvoys.dtmi_com_example_CustomTopicTokens__1;

namespace SampleClient;

public class RpcCommandRunner(MqttSessionClient mqttClient, IServiceProvider serviceProvider, ILogger<RpcCommandRunner> logger, IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        MqttConnectionSettings mcs = MqttConnectionSettings.FromConnectionString(configuration!.GetConnectionString("Default")! + ";ClientId=sampleClient-" + Environment.TickCount);

        await mqttClient.ConnectAsync(mcs, stoppingToken);
        await Console.Out.WriteLineAsync($"Connected to: {mcs}");


        string userResponse = "y";
        while (userResponse == "y")
        {
            await RunCustomTopicTokenCommand("SampleServerWithCustomTopicTokens");
            await Console.Out.WriteLineAsync("Run again? (y), type q to exit");
            userResponse = Console.ReadLine()!;
            if (userResponse == "q")
            {
                await mqttClient.DisposeAsync(); // This disconnects the mqtt client as well
                Environment.Exit(0);
            }
        }

        await mqttClient.DisconnectAsync();
    }

    private async Task RunMathCommands()
    {
        await using MathClient mathClient = serviceProvider.GetService<MathClient>()!;
        try
        {
            ExtendedResponse<GetRandomResponsePayload> respRandom = await mathClient.GetRandomAsync("SampleServer").WithMetadata();
            logger.LogInformation("getRandom = {r} with id {cid}", respRandom.Response!.GetRandomResponse, respRandom.ResponseMetadata!.CorrelationId);
            int number = respRandom.Response!.GetRandomResponse;

            CommandRequestMetadata reqMdIsPrime = new();
            Task<ExtendedResponse<IsPrimeResponsePayload>> respIsPrimeTask = mathClient.IsPrimeAsync("SampleServer",
                new IsPrimeRequestPayload
                {
                    IsPrimeRequest = new Object_IsPrime_Request
                    {
                        Number = number
                    }
                }, reqMdIsPrime).WithMetadata();

            logger.LogInformation("Calling isPrime({n}) with id {id}", number, reqMdIsPrime.CorrelationId);
            ExtendedResponse<IsPrimeResponsePayload> respIsPrime = await respIsPrimeTask;
            logger.LogInformation("Called isPrime({n}) = {p} with id {id}", number, respIsPrime.Response.IsPrimeResponse.IsPrime, respIsPrime.ResponseMetadata!.CorrelationId);

            CommandRequestMetadata reqMdFib = new();
            Task<ExtendedResponse<FibResponsePayload>> respFibTask = mathClient.FibAsync("SampleServer",
                new FibRequestPayload
                {
                    FibRequest = new Object_Fib_Request
                    {
                        Number = number
                    }
                }, reqMdFib, TimeSpan.FromSeconds(30)).WithMetadata();
            logger.LogInformation("Calling Fib({n}) with id {id}", number, reqMdFib.CorrelationId);

            ExtendedResponse<FibResponsePayload> respFib = await respFibTask;
            logger.LogInformation("Called Fib({n}) = {p} with id {id}", number, respFib.Response.FibResponse, respFib.ResponseMetadata!.CorrelationId);
        }
        catch (Exception ex)
        {
            logger.LogWarning("{msg}", ex.Message);
        }
    }

    private async Task RunGreeterCommands()
    {
        await using GreeterEnvoyClient greeterClient = serviceProvider.GetService<GreeterEnvoyClient>()!;
        try
        {
            CommandRequestMetadata reqMd = new();
            logger.LogInformation("Calling SayHello with id {id}", reqMd.CorrelationId);
            ExtendedResponse<GreeterEnvoy.HelloResponse> respGreet = await greeterClient.SayHello(
                new ExtendedRequest<GreeterEnvoy.HelloRequest>
                {
                    Request = new GreeterEnvoy.HelloRequest()
                    {
                        Name = "User"
                    }
                }, reqMd).WithMetadata();

            logger.LogInformation("greet {g} with id {id}", respGreet.Response!.Message, respGreet.ResponseMetadata!.CorrelationId);
        }
        catch (Exception ex)
        {
            logger.LogWarning("{msg}", ex.Message);
        }
    }

    private async Task RunCounterCommands(string server)
    {
        await using CounterClient counterClient = serviceProvider.GetService<CounterClient>()!;
        try
        {

            CommandRequestMetadata reqMd = new();

            logger.LogInformation("Calling ReadCounter with {c}", reqMd.CorrelationId);
            ExtendedResponse<ReadCounterResponsePayload> respCounter = await counterClient.ReadCounterAsync(server, reqMd).WithMetadata();
            logger.LogInformation("called read {c} with id {id}", respCounter.Response!.CounterResponse, respCounter.ResponseMetadata!.CorrelationId);


            Task[] tasks = new Task[32];
            for (int i = 0; i < tasks.Length; i++)
            {
                CommandRequestMetadata reqMd2 = new();
                IncrementRequestPayload payload = new IncrementRequestPayload();
                payload.IncrementValue = 1;
                logger.LogInformation("calling counter.incr  with id {id}", reqMd2.CorrelationId);
                Task<ExtendedResponse<IncrementResponsePayload>> incrCounterTask = counterClient.IncrementAsync(server, payload, reqMd2).WithMetadata();
                tasks[i] = incrCounterTask;
            }
            await Task.WhenAll(tasks);

            for (int i = 0; i < tasks.Length; i++)
            {
                Task<ExtendedResponse<IncrementResponsePayload>>? t = (Task<ExtendedResponse<IncrementResponsePayload>>?)tasks[i];
                logger.LogInformation("called counter.incr {c} with id {id}", t!.Result.Response.CounterResponse, t.Result.ResponseMetadata!.CorrelationId);
            }


            ExtendedResponse<ReadCounterResponsePayload> respCounter4 = await counterClient.ReadCounterAsync(server).WithMetadata();
            logger.LogInformation("counter {c} with id {id}", respCounter4.Response!.CounterResponse, respCounter4.ResponseMetadata!.CorrelationId);

        }
        catch (Exception ex)
        {
            logger.LogWarning("{msg}",ex.Message);
        }
    }

    private async Task RunCustomTopicTokenCommand(string server)
    {
        await using CustomTopicTokenClient customTopicTokenClient = serviceProvider.GetService<CustomTopicTokenClient>()!;
        try
        {

            CommandRequestMetadata reqMd = new();

            logger.LogInformation("Calling ReadCounter with {c}", reqMd.CorrelationId);
            ExtendedResponse<ReadCustomTopicTokenResponsePayload> respCounter = await customTopicTokenClient.ReadCustomTopicTokenAsync(server, reqMd).WithMetadata();
            logger.LogInformation("Received response with custom topic token {token}", respCounter.Response!.CustomTopicTokenResponse);
        }
        catch (Exception ex)
        {
            logger.LogWarning("{msg}", ex.Message);
        }
    }
}
