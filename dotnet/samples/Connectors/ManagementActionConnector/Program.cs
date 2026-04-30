// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Protocol;
using ManagementActionConnector;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton<ApplicationContext>();
        services.AddSingleton(MqttSessionClientProvider.Factory);
        services.AddSingleton<IMessageSchemaProvider, NoMessageSchemaProvider>();
        services.AddSingleton(AzureDeviceRegistryClientWrapperProvider.Factory);
        services.AddSingleton<IManagementActionHandlerFactory, SampleManagementActionHandlerFactory>();
        services.AddHostedService<ManagementActionConnectorWorkerSimplified>();
    })
    .Build();

host.Run();

