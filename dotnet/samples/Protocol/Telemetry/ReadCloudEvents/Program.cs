// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.SchemaRegistry;
using ReadCloudEventsSample;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services
    .AddSingleton<ApplicationContext>()
    .AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory)
    .AddTransient(SchemaRegistryClientFactoryProvider.SchemaRegistryFactory)
    .AddTransient<OvenClient>()
    .AddHostedService<Worker>();

IHost host = builder.Build();
host.Run();
