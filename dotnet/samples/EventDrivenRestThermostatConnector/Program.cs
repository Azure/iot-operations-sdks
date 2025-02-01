// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Microsoft.Extensions.DependencyInjection;
using RestThermostatConnector;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory);
        services.AddSingleton(RestThermostatDatasetSamplerFactory.RestDatasetSourceFactoryProvider);
        services.AddSingleton(AssetMonitorFactoryProvider.AssetMonitorFactory);
        services.AddHostedService<EventDrivenTelemetryConnectorWorker>();
        services.AddHostedService<ThermostatEventWorker>((serviceProvider) => new(null, serviceProvider.GetService<EventDrivenTelemetryConnectorWorker>()!));
    })
    .Build();

host.Run();
