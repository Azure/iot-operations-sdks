// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ConnectionManagementSample;
using SampleClient;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton(MqttClientFactoryProvider.OrderedAckFactory);
        services.AddHostedService<UserManagedConnectionWorker>();
    })
    .Build();

host.Run();
