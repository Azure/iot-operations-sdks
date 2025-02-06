// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using EventDrivenRestThermostatConnector;
using RestThermostatConnector;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory);
        services.AddSingleton(AssetMonitorFactoryProvider.AssetMonitorFactory);
        services.AddSingleton(DatasetMessageSchemaProviderFactory.DatasetMessageSchemaFactoryProvider);
        services.AddHostedService<EventDrivenRestThermostatConnectorWorker>();
    })
    .Build();

host.Run();
