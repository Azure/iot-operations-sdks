// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Protocol;
using PollingTelemetryConnectorTemplate;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton<ApplicationContext>();
        services.AddSingleton(MqttSessionClientProvider.Factory);
        services.AddSingleton(DatasetSamplerProvider.Factory);
        services.AddSingleton(MessageSchemaProvider.Factory);
        services.AddSingleton<IAdrClientWrapperProvider>(AdrClientWrapperProvider.Factory);
        services.AddSingleton(LeaderElectionConfigurationProvider.Factory); // If no leader election is needed, delete this line
        services.AddHostedService<PollingTelemetryConnectorWorker>();
    })
    .Build();

host.Run();
