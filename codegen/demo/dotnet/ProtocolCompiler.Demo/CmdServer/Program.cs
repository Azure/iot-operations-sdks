// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.RPC;
using Counters.CounterCollection;

namespace Server
{
    internal sealed class CounterCollectionService : CounterCollection.Service
    {
        private Dictionary<string, int> counterValues;
        private Dictionary<string, CounterLocation> counterLocations;

        public CounterCollectionService(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
            : base(applicationContext, mqttClient)
        {
            counterValues = new Dictionary<string, int>
            {
                { "alpha", 0 },
                { "beta", 0 },
            };

            counterLocations = new Dictionary<string, CounterLocation>
            {
                { "alpha", new CounterLocation { Latitude = 14.4, Longitude = -123.0 } },
            };
        }

        public override Task<ExtendedResponse<IncrementResponsePayload>> IncrementAsync(IncrementRequestPayload request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
        {
            ExtendedResponse<IncrementResponsePayload> extendedResponse;

            if (!counterValues.TryGetValue(request.CounterName, out int currentValue))
            {
                extendedResponse = ExtendedResponse<IncrementResponsePayload>.CreateFromResponse(new IncrementResponsePayload { CounterValue = 0 })
                    .WithApplicationError(ErrorCondition.CounterNotFound, new CounterList() { CounterNames = counterValues.Keys.ToList() });

                return Task.FromResult(extendedResponse);
            }

            if (currentValue == int.MaxValue)
            {
                extendedResponse = ExtendedResponse<IncrementResponsePayload>.CreateFromResponse(new IncrementResponsePayload { CounterValue = int.MaxValue })
                    .WithApplicationError(ErrorCondition.CounterOverflow);

                return Task.FromResult(extendedResponse);
            }

            int newValue = currentValue + 1;
            counterValues[request.CounterName] = newValue;

            extendedResponse = ExtendedResponse<IncrementResponsePayload>.CreateFromResponse(new IncrementResponsePayload { CounterValue = newValue });
            return Task.FromResult(extendedResponse);
        }

        public override Task<ExtendedResponse<GetLocationResponsePayload>> GetLocationAsync(GetLocationRequestPayload request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
        {
            if (!counterValues.ContainsKey(request.CounterName))
            {
                throw new CounterErrorException(new CounterError { Explanation = $"Dotnet counter '{request.CounterName}' not found in counter collection" })
                    .WithApplicationError(ErrorCondition.CounterNotFound, new CounterList { CounterNames = counterValues.Keys.ToList() });
            }

            counterLocations.TryGetValue(request.CounterName, out CounterLocation? counterLocation);

            return Task.FromResult(ExtendedResponse<GetLocationResponsePayload>.CreateFromResponse(new GetLocationResponsePayload { CounterLocation = counterLocation } ));
        }
    }

    internal sealed class Program
    {
        const string serverId = "DotnetCounterServer";

        static async Task Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: CmdServer seconds_to_run");
                return;
            }

            TimeSpan runDuration = TimeSpan.FromSeconds(int.Parse(args[0], CultureInfo.InvariantCulture));

            ApplicationContext appContext = new();
            MqttSessionClient mqttSessionClient = new();

            Console.Write($"Connecting to MQTT broker as {serverId} ... ");
            await mqttSessionClient.ConnectAsync(new MqttConnectionSettings("localhost", serverId) { TcpPort = 1883, UseTls = false });
            Console.WriteLine("Connected!");

            CounterCollectionService service = new(appContext, mqttSessionClient);

            Console.Write("Starting server ... ");
            await service.StartAsync();
            Console.WriteLine($"server running for {runDuration}");

            await Task.Delay(runDuration);

            Console.Write("Stopping server ... ");
            await service.StopAsync();
            Console.WriteLine("server stopped");
        }
    }
}
