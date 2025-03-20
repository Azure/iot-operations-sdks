// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Protocol;
using PollingTelemetryConnector;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton<ApplicationContext>();
        services.AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory);
        services.AddSingleton(DatasetSamplerFactory.DatasetSamplerFactoryProvider);
        services.AddSingleton(MessageSchemaProvider.MessageSchemaProviderFactory);
        services.AddSingleton(AssetMonitorFactoryProvider.AssetMonitorFactory);
        services.AddSingleton(LeaderElectionConfigurationProvider.ConnectorLeaderElectionConfigurationProviderFactory); // If no leader election is needed, delete this line
        services.AddHostedService<PollingTelemetryConnectorWorker>();
    })
    .Build();

host.Run();
