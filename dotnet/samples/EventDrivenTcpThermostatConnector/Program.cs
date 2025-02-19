// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using EventDrivenTcpThermostatConnector;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory);
        services.AddSingleton(AssetMonitorFactoryProvider.AssetMonitorFactory);
        services.AddSingleton(NoMessageSchemaProvider.NoMessageSchemaProviderFactory);
        services.AddHostedService<EventDrivenTcpThermostatConnectorWorker>();
    })
    .Build();

host.Run();
