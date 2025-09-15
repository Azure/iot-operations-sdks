// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using StreamingCommandClient;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory);


var host = builder.Build();
host.Run();
