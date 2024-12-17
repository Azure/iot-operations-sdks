// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.SchemaRegistry;
using SampleReadCloudEvents;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services
    .AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory)
    .AddTransient(SchemaRegistryClientFactoryProvider.SchemaRegistryFactory)
    .AddTransient<OvenClient>()
    .AddHostedService<Worker>();

IHost host = builder.Build();
host.Run();
