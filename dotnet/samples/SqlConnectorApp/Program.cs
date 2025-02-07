// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using SqlQualityAnalyzerConnectorApp;

IHost host = Host.CreateDefaultBuilder(args)

    .ConfigureServices(services =>
    {
        services.AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory);
        services.AddSingleton(SqlQualityAnalyzerDatasetSamplerFactory.DatasetSamplerFactoryProvider);
        services.AddSingleton(DatasetMessageSchemaProviderFactory.DatasetMessageSchemaFactoryProvider);
        services.AddSingleton(AssetMonitorFactoryProvider.AssetMonitorFactory);
        services.AddHostedService<PollingTelemetryConnectorWorker>();
    })
    .Build();

host.Run();
