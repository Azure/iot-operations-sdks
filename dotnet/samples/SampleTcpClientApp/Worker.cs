// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Sockets;
using System.Net;
using System.Text.Json;

namespace SampleTcpClientApp
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);

                try
                {
                    using TcpClient client = new();
                    await client.ConnectAsync("127.0.0.1", 80);
                    await using NetworkStream stream = client.GetStream();

                    RestThermostatConnector.ThermostatStatus thermostatStatus = new()
                    {
                        DesiredTemperature = 72.0,
                        CurrentTemperature = 70.0
                    };

                    byte[] payload = JsonSerializer.SerializeToUtf8Bytes(thermostatStatus);

                    await stream.WriteAsync(payload, 0, payload.Length, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to connect to TCP server");
                }   
            }
        }
    }
}
