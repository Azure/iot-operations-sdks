// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;
using EventDrivenApp;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddSingleton<ApplicationContext>()
    .AddTransient<SessionClientFactory>()
    .AddHostedService<InputWorker>()
    .AddHostedService<OutputWorker>();

builder.Build().Run();
