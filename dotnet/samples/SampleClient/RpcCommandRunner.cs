// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.RPC;
using TestEnvoys.CustomTopicTokens;
using System.Collections.ObjectModel;

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
            await RunCustomTopicTokenCommands("SampleServer");
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

    private async Task RunCustomTopicTokenCommands(string executorId)
    {
        await using CustomTopicTokenClient customTopicTokenClient = serviceProvider.GetService<CustomTopicTokenClient>()!;
        logger.LogInformation("client is null? " + (customTopicTokenClient == null));
        CommandRequestMetadata cmdMetadata = new();
        logger.LogInformation("topic tokens is null? " + (cmdMetadata.TopicTokens == null));
        
        Dictionary<string, string> transientTopicTokenMap = new Dictionary<string, string>();
        //transientTopicTokenMap["ex:myCustomTopicToken"] = "someCustomValue1";
        //transientTopicTokenMap["ex:commandName"] = "someCommandName1";
        transientTopicTokenMap["myCustomTopicToken"] = "someCustomValue2";
        transientTopicTokenMap["commandName"] = "someCommandName2";

        try
        {
            ExtendedResponse<ReadCustomTopicTokenResponsePayload> respCounter = await customTopicTokenClient.ReadCustomTopicTokenAsync(executorId, cmdMetadata, transientTopicTokenMap).WithMetadata();
            logger.LogInformation("Sent custom topic token request");
        }
        catch (Exception ex)
        {
            logger.LogWarning("{msg}", ex.Message);
        }
    }
}
