// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Mqtt.Session;

namespace CounterServer;

public class RpcHostBackgroundService(CounterService counterService) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await counterService.StartAsync(null, cancellationToken: stoppingToken);
    }

    protected ValueTask DisposeAsync() => counterService!.DisposeAsync();
}
