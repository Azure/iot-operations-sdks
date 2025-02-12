// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Sockets;
using System.Net;
using System.Text.Json;
using System.Text;

namespace SampleTcpServiceApp
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
                using var tcpListener = new TcpListener(System.Net.IPAddress.Any, 80);

                try
                {
                    _logger.LogInformation("Starting TCP listener");
                    tcpListener.Start();

                    _logger.LogInformation("Waiting for a TCP connection");
                    using TcpClient handler = await tcpListener.AcceptTcpClientAsync();

                    _logger.LogInformation("Accepted a TCP connection");

                    await using NetworkStream stream = handler.GetStream();

                    try
                    {
                        while (true)
                        {
                            // Wait a bit to simulate this information being sent as an event
                            await Task.Delay(TimeSpan.FromSeconds(3 + new Random().Next(5)));

                            ThermostatStatus thermostatStatus = new()
                            {
                                DesiredTemperature = new Random().NextDouble() * 30 + 50,
                                CurrentTemperature = new Random().NextDouble() * 30 + 50
                            };

                            string payload = JsonSerializer.Serialize(thermostatStatus);

                            _logger.LogInformation("Writing to TCP stream: {0}", payload);
                            byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
                            await stream.WriteAsync(payloadBytes, 0, payloadBytes.Length, stoppingToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to handle TCP connection");
                    }
                    finally
                    {
                        handler.Close();
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Failed to start listening for a TCP connection");
                    continue;
                }
                finally
                {
                    tcpListener.Stop();
                }
            }
        }
    }
}
