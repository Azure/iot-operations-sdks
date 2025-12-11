using System.Buffers;
using System.Text.Json;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
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

            string asset1TelemetryTopic = "mqtt/machine/asset1/status";
            TaskCompletionSource<MqttApplicationMessage> asset1TelemetryReceived = new();
            mqttClient.ApplicationMessageReceivedAsync += (args) =>
            {
                if (IsValidPayload(args.ApplicationMessage.Payload))
                {
                    if (args.ApplicationMessage.Topic.Equals(asset1TelemetryTopic))
                    {
                        asset1TelemetryReceived.TrySetResult(args.ApplicationMessage);
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
                var applicationMessage = await asset1TelemetryReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

                Assert.False(string.IsNullOrEmpty(GetCloudEventTimeFromMqttMessage(applicationMessage)));
                // CloudEvent source is built from endpoint address with ms-aio: prefix (Priority 2 in BuildSource)
                Assert.Equal("ms-aio:my-rest-thermostat-device-name/thermostat", GetCloudEventSourceFromMqttMessage(applicationMessage));
                string dataSchema = GetCloudEventDataSchemaFromMqttMessage(applicationMessage);
                Assert.Equal($"aio-sr://DefaultSRNamespace/A3E45EFE41FF52AC3BE2EA4E9FD7A33BE0D9ECCE487887765A7F2111A04E0BF0:1.0", dataSchema);
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

            await using AzureDeviceRegistryClient adrClient = new(new(), mqttClient);

            // A connector's status may fluctuate over time depending on if mqtt telemetry or state store requests
            // timeout. This retry logic allows for checking the status over the course of a few seconds to see
            // if it is okay at any point or if it reports an unexpected status consistently.
            int retryCount = 0;
            while (true)
            {
                try
                {
                    // Check that the device status was reported
                    DeviceStatus deviceStatus = await adrClient.GetDeviceStatusAsync("my-rest-thermostat-device-name", "my-rest-thermostat-endpoint-name");
                    Assert.NotNull(deviceStatus.Config);
                    Assert.Null(deviceStatus.Config.Error);
                    Assert.NotNull(deviceStatus.Config.LastTransitionTime);

                    // Check that both asset statuses were reported
                    AssetStatus asset1Status = await adrClient.GetAssetStatusAsync("my-rest-thermostat-device-name", "my-rest-thermostat-endpoint-name", "my-rest-thermostat-asset1");
                    AssetStatus asset2Status = await adrClient.GetAssetStatusAsync("my-rest-thermostat-device-name", "my-rest-thermostat-endpoint-name", "my-rest-thermostat-asset2");
                    Assert.NotNull(asset1Status.Config);
                    Assert.Null(asset1Status.Config.Error);
                    Assert.NotNull(asset1Status.Config.LastTransitionTime);
                    Assert.NotNull(asset1Status.Datasets);
                    Assert.Single(asset1Status.Datasets);
                    Assert.Equal("thermostat_status", asset1Status.Datasets.First().Name);
                    Assert.Null(asset1Status.Datasets.First().Error);

                    Assert.NotNull(asset2Status.Config);
                    Assert.Null(asset2Status.Config.Error);
                    Assert.NotNull(asset2Status.Config.LastTransitionTime);
                    Assert.NotNull(asset2Status.Datasets);
                    Assert.Single(asset2Status.Datasets);
                    Assert.Equal("thermostat_status", asset2Status.Datasets.First().Name);
                    Assert.Null(asset2Status.Datasets.First().Error);
                    break;
                }
                catch (Exception)
                {
                    if (++retryCount > 5)
                    {
                        throw;
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(10));
            }
        }

        [Fact]
        public async Task TestDeployedEventDrivenTcpThermostatConnector()
        {
            await using var mqttClient = await ClientFactory.CreateSessionClientFromEnvAsync();

            string assetTelemetryTopic = "mqtt/machine/status/change_event";
            TaskCompletionSource<MqttApplicationMessage> assetTelemetryReceived = new();
            mqttClient.ApplicationMessageReceivedAsync += (args) =>
            {
                if (IsValidPayload(args.ApplicationMessage.Payload))
                {
                    if (args.ApplicationMessage.Topic.Equals(assetTelemetryTopic))
                    {
                        assetTelemetryReceived.TrySetResult(args.ApplicationMessage);
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
                var applicationMessage = await assetTelemetryReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

                Assert.False(string.IsNullOrEmpty(GetCloudEventTimeFromMqttMessage(applicationMessage)));
                // CloudEvent source is built from endpoint address with ms-aio: prefix (Priority 2 in BuildSource)
                Assert.Equal("ms-aio:my-tcp-thermostat/80", GetCloudEventSourceFromMqttMessage(applicationMessage));
                string dataSchema = GetCloudEventDataSchemaFromMqttMessage(applicationMessage);
                Assert.Equal($"aio-sr://DefaultSRNamespace/A3E45EFE41FF52AC3BE2EA4E9FD7A33BE0D9ECCE487887765A7F2111A04E0BF0:1.0", dataSchema);
            }
            catch (TimeoutException)
            {
                Assert.Fail("Timed out waiting for TCP connector telemetry to reach MQTT broker. This likely means the connector did not deploy successfully");
            }

            await using AzureDeviceRegistryClient adrClient = new(new(), mqttClient);

            // A connector's status may fluctuate over time depending on if mqtt telemetry or state store requests
            // timeout. This retry logic allows for checking the status over the course of a few seconds to see
            // if it is okay at any point or if it reports an unexpected status consistently.
            int retryCount = 0;
            while (true)
            {
                try
                {
                    // Check that the device status was reported
                    DeviceStatus deviceStatus = await adrClient.GetDeviceStatusAsync("my-tcp-thermostat", "my_tcp_endpoint");
                    Assert.NotNull(deviceStatus.Config);
                    Assert.Null(deviceStatus.Config.Error);
                    Assert.NotNull(deviceStatus.Config.LastTransitionTime);

                    // Check that both asset statuses were reported
                    AssetStatus assetStatus = await adrClient.GetAssetStatusAsync("my-tcp-thermostat", "my_tcp_endpoint", "my-tcp-thermostat-asset");
                    Assert.NotNull(assetStatus.Config);
                    Assert.Null(assetStatus.Config.Error);
                    Assert.NotNull(assetStatus.Config.LastTransitionTime);
                    Assert.NotNull(assetStatus.EventGroups);
                    Assert.Single(assetStatus.EventGroups);
                    var eventGroupStatus = assetStatus.EventGroups.First();
                    Assert.Equal("my-event-group", eventGroupStatus.Name);
                    Assert.NotNull(eventGroupStatus.Events);
                    Assert.Single(eventGroupStatus.Events);
                    var eventStatus = eventGroupStatus.Events.First();
                    Assert.Equal("thermostat_status_changed", eventStatus.Name);
                    Assert.Null(eventStatus.Error);
                    break;
                }
                catch (Exception)
                {
                    if (++retryCount > 5)
                    {
                        throw;
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(10));
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

        private static string SafeGetUserProperty(MqttApplicationMessage mqttMessage, string name)
        {
            if (mqttMessage.UserProperties == null)
            {
                return "";
            }

            foreach (MqttUserProperty userProperty in mqttMessage.UserProperties)
            {
                if (userProperty.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return userProperty.Value;
                }
            }

            return "";
        }

        private static string GetCloudEventSourceFromMqttMessage(MqttApplicationMessage mqttMessage)
        {
            return SafeGetUserProperty(mqttMessage, nameof(CloudEvent.Source));
        }

        private static string GetCloudEventTimeFromMqttMessage(MqttApplicationMessage mqttMessage)
        {
            return SafeGetUserProperty(mqttMessage, nameof(CloudEvent.Time));
        }

        private static string GetCloudEventDataSchemaFromMqttMessage(MqttApplicationMessage mqttMessage)
        {
            return SafeGetUserProperty(mqttMessage, nameof(CloudEvent.DataSchema));
        }

        private bool IsValidPayload(ReadOnlySequence<byte> payload)
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
