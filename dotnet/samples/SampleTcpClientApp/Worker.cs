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
                var tcpListener = new TcpListener(System.Net.IPAddress.Any, 80);

                try
                {
                    _logger.LogInformation("Starting TCP listener");
                    tcpListener.Start();
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Failed to start listening for a TCP connection");
                    continue;
                }

                try
                {
                    _logger.LogInformation("Waiting for a TCP connection");
                    using TcpClient handler = await tcpListener.AcceptTcpClientAsync();

                    _logger.LogInformation("Accepted a TCP connection");


                    await using NetworkStream stream = handler.GetStream();

                    ThermostatStatus thermostatStatus = new()
                    {
                        DesiredTemperature = 72.0,
                        CurrentTemperature = 70.0
                    };

                    byte[] payload = JsonSerializer.SerializeToUtf8Bytes(thermostatStatus);

                    _logger.LogInformation("Writing to TCP stream");
                    await stream.WriteAsync(payload, 0, payload.Length, stoppingToken);
                    handler.Close();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to handle TCP connection");
                }   
            }
        }
    }
}
