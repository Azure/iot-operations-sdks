// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using SampleTcpClientApp;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
