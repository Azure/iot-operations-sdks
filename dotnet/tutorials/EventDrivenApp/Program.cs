// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using EventDrivenApp;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddSingleton(MqttClientFactoryProvider.MqttConnectionSettingsFactory)
    .AddTransient(MqttClientFactoryProvider.MqttSessionClientFactory)
    .AddHostedService<InputWorker>()
    .AddHostedService<OutputWorker>();

builder.Build().Run();
