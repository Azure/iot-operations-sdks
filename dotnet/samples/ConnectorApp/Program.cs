// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using HttpConnectorWorkerService;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory);
        services.AddHostedService<Worker>();
    })
    .Build();

host.Run();
