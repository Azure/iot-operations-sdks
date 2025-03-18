// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using SampleTcpServiceApp;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
