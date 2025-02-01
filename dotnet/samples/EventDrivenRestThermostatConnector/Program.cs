// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using RestThermostatConnector;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory);
        services.AddSingleton(RestThermostatDatasetSamplerFactory.RestDatasetSourceFactoryProvider);
        services.AddSingleton(AssetMonitorFactoryProvider.AssetMonitorFactory);
        services.AddSingleton<EventDrivenTelemetryConnectorWorker>();
        services.AddSingleton<ThermostatEventWorker>((serviceProvider) => new(serviceProvider.GetRequiredService<EventDrivenTelemetryConnectorWorker>()));
    })
    .Build();

host.Run();
