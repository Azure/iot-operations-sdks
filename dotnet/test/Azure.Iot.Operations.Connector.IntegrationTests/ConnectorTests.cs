using System.Buffers;
using System.Text.Json;
using Azure.Iot.Operations.Services.StateStore;
using Xunit;

namespace Azure.Iot.Operations.Connector.IntegrationTests
{
    // Note that these tests can only be run once the sample connectors have been deployed. These tests check that
    // the connector's output is directed to the expected MQTT topic/the expected DSS key.
    public class ConnectorTests
    {
        [Fact]
        public async Task TestDeployedPollingRestThermostatConnector()
        {
            await using var mqttClient = await ClientFactory.CreateSessionClientFromEnvAsync();

            string asset1TelemetryTopic = "/mqtt/machine/asset1/status";
            TaskCompletionSource asset1TelemetryReceived = new();
            mqttClient.ApplicationMessageReceivedAsync += (args) =>
            {
                if (isValidPayload(args.ApplicationMessage.Payload))
                {
                    if (args.ApplicationMessage.Topic.Equals(asset1TelemetryTopic))
                    {
                        asset1TelemetryReceived.TrySetResult();
                    }
                }

                return Task.CompletedTask;
            };

            await mqttClient.SubscribeAsync(new Protocol.Models.MqttClientSubscribeOptions()
            {
                TopicFilters = new()
                {
                    new(asset1TelemetryTopic),
                }
            });

            try
            {
                await asset1TelemetryReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (TimeoutException)
            {
                Assert.Fail("Timed out waiting for polling telemetry connector telemetry to reach MQTT broker. This likely means the connector did not deploy successfully");
            }

            await using StateStoreClient stateStoreClient = new(new(), mqttClient);

            string expectedStateStoreKey = "RestThermostatKey";
            TaskCompletionSource stateStoreUpdatedByConnectorAsset2Tcs = new();
            stateStoreClient.KeyChangeMessageReceivedAsync += (sender, args) =>
            {
                if (args.ChangedKey.ToString().Equals(expectedStateStoreKey))
                {
                    stateStoreUpdatedByConnectorAsset2Tcs.TrySetResult();
                }
                return Task.CompletedTask;
            };

            await stateStoreClient.ObserveAsync(expectedStateStoreKey);

            try
            {
                await stateStoreUpdatedByConnectorAsset2Tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (TimeoutException)
            {
                Assert.Fail("Timed out waiting for polling telemetry connector to push expected data to DSS. This likely means the connector did not deploy successfully");
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


        [Fact (Skip = "SQL server deployment is flakey, so this test is flakey")]
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
