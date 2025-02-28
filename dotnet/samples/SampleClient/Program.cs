// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using SampleClient;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton<ApplicationContext>();
        services.AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory);
        services.AddHostedService<RpcCommandRunner>();
        services.AddTransient<GreeterEnvoyClient>();
        services.AddTransient<MathClient>();
        services.AddTransient(CounterClient.Factory);
        services.AddTransient<MemMonClient>();
    })
    .Build();

host.Run();
