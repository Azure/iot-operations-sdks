// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using SampleCloudEvents;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services
    .AddSingleton<ApplicationContext>()
    .AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory)
    .AddTransient(SchemaRegistryClientFactoryProvider.SchemaRegistryFactory)
    .AddTransient<OvenService>()
    .AddHostedService<Worker>();

IHost host = builder.Build();
host.Run();
