// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using SimpleRpc;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
