// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.RPC;
using MQTTnet.Client;
using TestEnvoys.dtmi_com_example_Counter__1;

namespace CounterClient;

public class RpcCommandRunner(MqttSessionClient mqttClient, CounterClient counterClient, ILogger<RpcCommandRunner> logger, IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {

            MqttConnectionSettings mcs = MqttConnectionSettings.FromConnectionString(configuration!.GetConnectionString("Default")! + ";ClientId=sampleClient-" + Environment.TickCount);

            await mqttClient.ConnectAsync(mcs, stoppingToken);
            await Console.Out.WriteLineAsync($"Connected to: {mcs}");
            var server_id = configuration.GetValue<string>("COUNTER_SERVER_ID") ?? "CounterServer";
            await counterClient.StartAsync(stoppingToken);
            await RunCounterCommands(server_id);
            await mqttClient.DisconnectAsync();
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
            Environment.Exit(1);
        }
    }


    private async Task RunCounterCommands(string server)
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

        if (!IsTelemetryCountEqualTo(tasks.LongLength))

        {
            throw new Exception("Telemetry count mismatch");
        }
    }

    private bool IsTelemetryCountEqualTo(long targetCount)
    {
        long currentCount = counterClient.GetTelemetryCount();
        logger.LogInformation($"Current telemetry count: {currentCount}");
        return currentCount == targetCount;
    }
}
