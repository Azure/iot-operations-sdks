// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Protocol;
using ManagementActionConnector;
using ManagementActionConnector.Devices;
using ManagementActionConnector.Handlers;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton<ApplicationContext>();
        services.AddSingleton(MqttSessionClientProvider.Factory);
        services.AddSingleton<IMessageSchemaProvider, NoMessageSchemaProvider>();
        services.AddSingleton(AzureDeviceRegistryClientWrapperProvider.Factory);

        // Shared in-process simulator stands in for a real southbound device.
        services.AddSingleton<FakeDevice>();
        services.AddSingleton<IManagementActionHandlerFactory, SampleManagementActionHandlerFactory>();

        services.AddHostedService<ManagementActionConnectorWorkerSample>();
    })
    .Build();

host.Run();
