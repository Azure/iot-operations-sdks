// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Protocol;
using EventDrivenTelemetryConnector;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton<ApplicationContext>();
        services.AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory);
        services.AddSingleton(MessageSchemaProvider.MessageSchemaProviderFactory);
        services.AddSingleton(AssetMonitorFactoryProvider.AssetMonitorFactory);
        services.AddSingleton(new ConnectorLeaderElectionConfigurationProvider(new("some-leadership-position-id", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(9))));
        services.AddHostedService<ConnectorWorker>();
    })
    .Build();

host.Run();
