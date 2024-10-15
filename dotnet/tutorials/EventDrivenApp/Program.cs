// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using EventDrivenApp;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddTransient<SessionClientFactory>()
    .AddHostedService<InputWorker>()
    .AddHostedService<OutputWorker>();

builder.Build().Run();
