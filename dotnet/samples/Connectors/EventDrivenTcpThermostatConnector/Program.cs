// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Protocol;
using EventDrivenTcpThermostatConnector;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton<ApplicationContext>();
        services.AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory);
        services.AddSingleton(AssetMonitorFactoryProvider.AssetMonitorFactory);
        services.AddSingleton(NoMessageSchemaProvider.NoMessageSchemaProviderFactory);
        services.AddSingleton(new ConnectorLeaderElectionConfigurationProvider(new("some-tcp-leadership-position-id", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(9))));
        services.AddHostedService<EventDrivenTcpThermostatConnectorWorker>();
    })
    .Build();

host.Run();
