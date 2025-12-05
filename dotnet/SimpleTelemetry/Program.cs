// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using SimpleTelemetry;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
