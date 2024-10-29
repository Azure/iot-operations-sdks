// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.GenericHttpConnectorSample;
using HttpThermostatConnectorApp;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory);
        services.AddSingleton(ThermostatDatasetSamplerFactory.ThermostatDatasetSamplerFactoryProvider);
        services.AddHostedService<GenericConnectorWorkerService>();
    })
    .Build();

host.Run();
