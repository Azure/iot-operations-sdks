// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Telemetry;
using Azure.Iot.Operations.Services.AzureDeviceRegistry;
using Azure.Iot.Operations.Services.SchemaRegistry;
using System.Text;
using System.Text.Json;

namespace DotnetHttpConnectorWorkerService
{
    public class StringTelemetrySender : TelemetrySender<string>
    {
        public StringTelemetrySender(IMqttPubSubClient mqttClient)
            : base(mqttClient, "test", new Utf8JsonSerializer())
        {
        }
    }

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
                Console.WriteLine("Successfully created ADR client");

                string assetId = "todo - doesn't matter yet";
                AssetEndpointProfile aep = await adrClient.GetAssetEndpointProfileAsync(assetId);
                Console.WriteLine("Successfully retrieved asset endpoint profile");

                if (aep.AdditionalConfiguration == null)
                {
                    throw new InvalidOperationException("Expected some additional configuration field in the asset endpoint profile");
                }

                string httpPath = aep.AdditionalConfiguration!.RootElement.GetProperty("HttpPath")!.GetString()!;

                if (aep.Credentials == null || aep.Credentials.Username == null || aep.Credentials.Password == null)
                { 
                    throw new InvalidOperationException("Expected an asset endpoint username and password.");
                }

                HttpDataRetriever httpDataRetriever = new(aep.TargetAddress, httpPath, aep.Credentials.Username, aep.Credentials.Password);

                while (true)
                {
                    // Read data from the 3rd party asset
                    string httpData = await httpDataRetriever.RetrieveDataAsync();

                    Console.WriteLine("Read data from http asset endpoint:");
                    Console.WriteLine(httpData);
                    Console.WriteLine();

                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            }
        }
    }
}
