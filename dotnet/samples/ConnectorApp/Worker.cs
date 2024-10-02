// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Services.AzureDeviceRegistry;
using System.Text.Json;

namespace DotnetHttpConnectorWorkerService
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
                // ADR client stub
                AzureDeviceRegistryClient adrClient = new();
                string assetId = "todo - doesn't matter yet";
                AssetEndpointProfile aep = await adrClient.GetAssetEndpointProfileAsync(assetId);
                JsonDocument? additionalConfiguration = null;

                HttpDataRetriever httpDataRetriever = new(aep.TargetAddress, "todo", aep.Credentials?.Username ?? "", aep.Credentials?.Password ?? Array.Empty<byte>());

                MqttConnectionSettings mqttConnectionSettings = null;
                MqttSessionClient sessionClient = null;

                await sessionClient.ConnectAsync(mqttConnectionSettings);

                while (true)
                {
                    // Read data from the 3rd party asset
                    string httpData = await httpDataRetriever.RetrieveDataAsync();
                    
                    // Send that data to the Azure IoT Operations broker
                    await sessionClient.PublishAsync(new MqttApplicationMessage("todo"));

                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            }
        }
    }
}
