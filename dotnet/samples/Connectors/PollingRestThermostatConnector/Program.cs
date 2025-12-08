// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Connector.ConnectorConfigurations;
using Azure.Iot.Operations.Protocol;
using PollingRestThermostatConnector;
using RestThermostatConnector;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton<ApplicationContext>();
        services.AddSingleton(MqttSessionClientProvider.Factory);
        services.AddSingleton(RestThermostatDatasetSamplerProvider.Factory);
        services.AddSingleton(MessageSchemaProvider.Factory);
        services.AddSingleton(LeaderElectionConfigurationProvider.Factory);
        services.AddSingleton<IAdrClientWrapperProvider>(AdrClientWrapperProvider.Factory);
        services.AddHostedService<PollingTelemetryConnectorWorker>();
    })
    .Build();

host.Run();
