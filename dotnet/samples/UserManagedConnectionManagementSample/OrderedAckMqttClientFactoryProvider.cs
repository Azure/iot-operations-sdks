﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt;
using MQTTnet;
using System.Diagnostics;

namespace SampleClient;

internal static class MqttClientFactoryProvider
{
    public static Func<IServiceProvider, OrderedAckMqttClient> OrderedAckFactory = service =>
    {
        IConfiguration? config = service.GetService<IConfiguration>();
        bool mqttDiag = config!.GetValue<bool>("mqttDiag");

        if (mqttDiag)
        {
            Trace.Listeners.Add(new ConsoleTraceListener());
            return new OrderedAckMqttClient(new MqttClientFactory().CreateMqttClient(MqttNetTraceLogger.CreateTraceLogger()));
        }
        else
        {
            return new OrderedAckMqttClient(new MqttClientFactory().CreateMqttClient());
        }
    };
}
