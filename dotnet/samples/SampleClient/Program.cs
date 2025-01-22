﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using SampleClient;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory);
        services.AddHostedService<RpcCommandRunner>();
        services.AddTransient<GreeterEnvoyClient>();
        services.AddTransient<MathClient>();
        services.AddTransient(CounterClient.Factory);
        services.AddTransient<MemMonClient>();
        services.AddTransient<CustomTopicTokenCounterClient>();
    })
    .Build();

host.Run();