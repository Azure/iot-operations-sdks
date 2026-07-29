// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using System.Diagnostics;

namespace CounterServer;

internal static class MqttSessionClientFactoryProvider
{
    public static Func<IServiceProvider, MqttSessionClient> MqttSessionClientFactory = service =>
    {
        IConfiguration? config = service.GetService<IConfiguration>();
        bool mqttDiag = config!.GetValue<bool>("mqttDiag");
        MqttSessionClientOptions sessionClientOptions = new()
        {
            EnableMqttLogging = mqttDiag,
        };

        if (mqttDiag)
        {
            Trace.Listeners.Add(new ConsoleTraceListener());
        }
        MqttConnectionSettings mcs = MqttConnectionSettings.FromEnvVars();
        MqttSessionClient mqttClient = new MqttSessionClient(sessionClientOptions);
        MqttClientConnectResult connAck = mqttClient.ConnectAsync(mcs).Result;
        
        return mqttClient;
    };
}
