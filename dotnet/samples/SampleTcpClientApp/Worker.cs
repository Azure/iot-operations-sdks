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
                try
                {
                    var tcpListener = new TcpListener(System.Net.IPAddress.Any, 80);
                    _logger.LogInformation("Starting TCP listener");
                    tcpListener.Start();

                    using TcpClient handler = await tcpListener.AcceptTcpClientAsync();
                    await handler.ConnectAsync("127.0.0.1", 80);

                    await using NetworkStream stream = handler.GetStream();

                    ThermostatStatus thermostatStatus = new()
                    {
                        DesiredTemperature = 72.0,
                        CurrentTemperature = 70.0
                    };

                    byte[] payload = JsonSerializer.SerializeToUtf8Bytes(thermostatStatus);

                    _logger.LogInformation("Writing to TCP stream");
                    await stream.WriteAsync(payload, 0, payload.Length, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to open TCP connection");
                }   
            }
        }
    }
}
