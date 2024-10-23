// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.ConnectorSample;
using Azure.Iot.Operations.GenericHttpConnectorSample;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory);
        services.AddHostedService<GenericConnectorWorkerService>();
    })
    .Build();

host.Run();
