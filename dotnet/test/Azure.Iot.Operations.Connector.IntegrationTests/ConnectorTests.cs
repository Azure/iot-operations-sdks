using System.Buffers;
using System.Text.Json;
using Azure.Iot.Operations.Services.StateStore;
using Xunit;

namespace Azure.Iot.Operations.Connector.IntegrationTests
{
    // Note that these tests can only be run once the polling rest thermostat connector has been deployed. These tests check that
    // the connector's output is directed to the right MQTT topic.
    public class ConnectorTests
    {
        [Fact]
        public async Task TestDeployedPollingRestThermostatConnector()
        {
            await using var mqttClient = await ClientFactory.CreateSessionClientFromEnvAsync();

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

            try
            {
                await asset1TelemetryReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
                await asset2TelemetryReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (TimeoutException)
            {
                Assert.Fail("Timed out waiting for polling telemetry connector telemetry to reach MQTT broker. This likely means the connector did not deploy successfully");
            }
        }

        [Fact]
        public async Task TestDeployedEventDrivenTcpThermostatConnector()
        {
            await using var mqttClient = await ClientFactory.CreateSessionClientFromEnvAsync();

            string assetTelemetryTopic = "/mqtt/machine/status/change_event";
            TaskCompletionSource assetTelemetryReceived = new();
            mqttClient.ApplicationMessageReceivedAsync += (args) =>
            {
                if (isValidPayload(args.ApplicationMessage.Payload))
                {
                    if (args.ApplicationMessage.Topic.Equals(assetTelemetryTopic))
                    {
                        assetTelemetryReceived.TrySetResult();
                    }
                }

                return Task.CompletedTask;
            };

            await mqttClient.SubscribeAsync(new Protocol.Models.MqttClientSubscribeOptions()
            {
                TopicFilters = new()
                {
                    new(assetTelemetryTopic),
                }
            });

            try
            {
                await assetTelemetryReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (TimeoutException)
            {
                Assert.Fail("Timed out waiting for TCP connector telemetry to reach MQTT broker. This likely means the connector did not deploy successfully");
            }
        }


        [Fact (Skip = "Operator CRD bug doesn't allow for dataset destination to be DSS")]
        public async Task TestDeployedSqlConnector()
        {
            await using var mqttClient = await ClientFactory.CreateSessionClientFromEnvAsync();
            await using StateStoreClient stateStoreClient = new(new(), mqttClient);


            string expectedStateStoreKey = "SqlServerSampleKey";
            TaskCompletionSource stateStoreUpdatedByConnectorTcs = new();
            stateStoreClient.KeyChangeMessageReceivedAsync += (sender, args) =>
            {
                if (args.ChangedKey.ToString().Equals(expectedStateStoreKey))
                {
                    stateStoreUpdatedByConnectorTcs.TrySetResult();
                }
                return Task.CompletedTask;
            };

            await stateStoreClient.ObserveAsync(expectedStateStoreKey);

            try
            {
                await stateStoreUpdatedByConnectorTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (TimeoutException)
            {
                Assert.Fail("Timed out waiting for SQL connector to push expected data to DSS. This likely means the connector did not deploy successfully");
            }
        }

        private bool isValidPayload(ReadOnlySequence<byte> payload)
        {
            try
            {
                ThermostatStatus? status = JsonSerializer.Deserialize<ThermostatStatus>(payload.ToArray());

                if (status == null)
                {
                    return false;
                }

                return status.CurrentTemperature >= 67
                    && status.CurrentTemperature <= 78
                    && status.DesiredTemperature >= 67
                    && status.DesiredTemperature <= 78;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
