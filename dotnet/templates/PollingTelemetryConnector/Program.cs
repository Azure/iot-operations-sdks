// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using ConnectorApp;
using PollingTelemetryConnector;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory);
        services.AddSingleton(DatasetSamplerFactory.DatasetSamplerFactoryProvider);
        services.AddSingleton(DatasetMessageSchemaProviderFactory.DatasetMessageSchemaFactoryProvider);
        services.AddSingleton(AssetMonitorFactoryProvider.AssetMonitorFactory);
        services.AddHostedService<PollingTelemetryConnectorWorker>();
    })
    .Build();

host.Run();
