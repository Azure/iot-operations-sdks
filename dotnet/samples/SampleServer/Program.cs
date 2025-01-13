// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using SampleServer;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory);
        services.AddHostedService<RpcHostBackgroundService>();
        services.AddTransient<CounterService>();
        services.AddTransient<MathService>();
        services.AddTransient<GreeterService>();
        services.AddTransient<MemMonService>();
    })
    .Build();

host.Run();
