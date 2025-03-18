// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using System.Diagnostics;

namespace SampleReadCloudEvents;

internal class MqttSessionClientFactoryProvider
{
    public static Func<IServiceProvider, MqttSessionClient> MqttSessionClientFactory = service =>
    {
        IConfiguration? config = service.GetService<IConfiguration>();
        bool mqttDiag = config!.GetValue<bool>("mqttDiag");
        ILogger logger = LoggerFactory.Create(builder => builder.AddConfiguration(config!)).CreateLogger<Worker>();
        if (mqttDiag)
        {
            Trace.Listeners.Add(new ConsoleTraceListener());
        }

        MqttSessionClient sessionClient = new(new MqttSessionClientOptions() { EnableMqttLogging = mqttDiag });

        sessionClient.DisconnectedAsync += d =>
        {
            logger.LogWarning($"Session Client Disconnected {d.Reason}");
            return Task.CompletedTask;
        };

        sessionClient.SessionLostAsync += l =>
        {
            logger.LogWarning($"Session Client Lost {l.Reason}");
            Environment.Exit(1);
            return Task.CompletedTask;
        };

        return sessionClient;
    };

}
