// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using SampleCloudEvents;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services
    .AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory)
    .AddTransient(SchemaRegistryClientFactoryProvider.SchemaRegistryFactory)
    .AddTransient<OvenService>()
    .AddHostedService<Worker>();

IHost host = builder.Build();
host.Run();
