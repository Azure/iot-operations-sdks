// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CounterClient;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory);
        services.AddHostedService<RpcCommandRunner>();
        services.AddTransient(CounterClient.CounterClient.Factory);
    })
    .Build();

host.Run();