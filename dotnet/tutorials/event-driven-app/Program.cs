// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using EventDrivenApp;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<WindowFunctionWorker>();
builder.Services.AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory);

var host = builder.Build();
host.Run();
