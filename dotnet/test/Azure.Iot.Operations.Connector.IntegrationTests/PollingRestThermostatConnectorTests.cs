using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Azure.Iot.Operations.Protocol.IntegrationTests;
using Xunit;

namespace Azure.Iot.Operations.Connector.IntegrationTests
{
    public class PollingRestThermostatConnectorTests
    {
        [Fact]
        public async Task Connector()
        {
            var mqttClient = await ClientFactory.CreateSessionClientFromEnvAsync();

            string asset1TelemetryTopic = "/mqtt/machine/asset1/status";
            string asset2TelemetryTopic = "/mqtt/machine/asset2/status";
            TaskCompletionSource asset1TelemetryReceived = new();
            TaskCompletionSource asset2TelemetryReceived = new();
            mqttClient.ApplicationMessageReceivedAsync += (args) =>
            {
                if (isValidPayload(args.ApplicationMessage.Payload))
                {
                    if (args.ApplicationMessage.Topic.Equals(asset1TelemetryTopic))
                    {
                        asset1TelemetryReceived.TrySetResult();
                    }
                    else if (args.ApplicationMessage.Topic.Equals(asset2TelemetryTopic))
                    {
                        asset2TelemetryReceived.TrySetResult();
                    }
                }

                return Task.CompletedTask;
            };

            await mqttClient.SubscribeAsync(new Protocol.Models.MqttClientSubscribeOptions()
            {
                TopicFilters = new()
                {
                    new(asset1TelemetryTopic),
                    new(asset2TelemetryTopic),
                }
            });

            await asset1TelemetryReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await asset2TelemetryReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }

        private bool isValidPayload(ReadOnlySequence<byte> payload)
        {
            try
            {
                ThermostatStatus? status = JsonSerializer.Deserialize<ThermostatStatus>(payload.ToArray());

                return status != null;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
