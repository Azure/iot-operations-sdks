// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Protocol;
using EventDrivenTcpThermostatConnector;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton<ApplicationContext>();
        services.AddSingleton(MqttSessionClientProvider.Factory);
        services.AddSingleton(MessageSchemaProvider.Factory);
        services.AddSingleton(LeaderElectionConfigurationProvider.Factory);
        services.AddSingleton<IAdrClientWrapperProvider>(CustomAdrClientWrapperProvider.Factory);
        services.AddHostedService<EventDrivenTcpThermostatConnectorWorker>();
    })
    .Build();

host.Run();
