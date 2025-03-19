// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using System.Diagnostics;

namespace SampleClient;

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

        return new MqttSessionClient(sessionClientOptions);
    };
}
