// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using SessionClientSample;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory);
        services.AddHostedService<LibraryManagedConnectionWorker>();
    })
    .Build();

host.Run();
