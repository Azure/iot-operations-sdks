// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CounterServer;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory);
        services.AddTransient<CounterService>();
        services.AddHostedService<RpcHostBackgroundService>();
    })
    .Build();

host.Run();
