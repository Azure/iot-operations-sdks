// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Mqtt;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Retry;
using System.Diagnostics;

namespace Azure.Iot.Operations.Protocol.IntegrationTests
{
    public class ClientFactory
    {
        public static async Task<OrderedAckMqttClient> CreateClientAsyncFromEnvAsync(string clientId, bool withTraces = false, CancellationToken cancellationToken = default)
        {
            Debug.Assert(Environment.GetEnvironmentVariable("MQTT_TEST_BROKER_CS") != null);
            string cs = $"{Environment.GetEnvironmentVariable("MQTT_TEST_BROKER_CS")}";
            MqttConnectionSettings mcs = MqttConnectionSettings.FromConnectionString(cs);
            if (string.IsNullOrEmpty(clientId))
            {
                mcs.ClientId += Guid.NewGuid();
            }
            else
            {
                mcs.ClientId = clientId;
            }

            MQTTnet.IMqttClient mqttClient = withTraces
                ? new MQTTnet.MqttClientFactory().CreateMqttClient(MqttNetTraceLogger.CreateTraceLogger())
                : new MQTTnet.MqttClientFactory().CreateMqttClient();
            var orderedAckClient = new OrderedAckMqttClient(mqttClient);
            await orderedAckClient.ConnectAsync(new MqttClientOptions(mcs), cancellationToken);

            return orderedAckClient;
        }

        public static async Task<MqttSessionClient> CreateSessionClientForFaultableBrokerFromEnv(List<MqttUserProperty>? ConnectUserProperties = null, string? clientId = null)
        {
            if (string.IsNullOrEmpty(clientId))
            {
                clientId = Guid.NewGuid().ToString();
            }
            string cs = Environment.GetEnvironmentVariable("FAULTABLE_MQTT_TEST_BROKER_CS")!;
            MqttConnectionSettings mcs = MqttConnectionSettings.FromConnectionString(cs);
            mcs.ClientId = clientId;
            MqttSessionClientOptions sessionClientOptions = new MqttSessionClientOptions()
            {
                // This retry policy prevents the client from retrying forever
                ConnectionRetryPolicy = new ExponentialBackoffRetryPolicy(10, TimeSpan.FromSeconds(5)),
                RetryOnFirstConnect = true, // This helps counteract if MQ is still deploying when the test is run
                EnableMqttLogging = true,
            };

            var sessionClient = new MqttSessionClient(sessionClientOptions);

            MqttClientOptions clientOptions = new MqttClientOptions(mcs);
            if (ConnectUserProperties != null)
            {
                foreach (var property in ConnectUserProperties)
                {
                    clientOptions.AddUserProperty(property.Name, property.Value);
                }
            }

            await sessionClient.ConnectAsync(clientOptions);
            return sessionClient;
        }

        public static async Task<MqttSessionClient> CreateSessionClientFromEnvAsync(string clientId = "")
        {
            Debug.Assert(Environment.GetEnvironmentVariable("MQTT_TEST_BROKER_CS") != null);
            string cs = Environment.GetEnvironmentVariable("MQTT_TEST_BROKER_CS")!;

            MqttConnectionSettings mcs = MqttConnectionSettings.FromConnectionString(cs);
            if (string.IsNullOrEmpty(clientId))
            {
                mcs.ClientId += Guid.NewGuid();
            }
            else
            {
                mcs.ClientId = clientId;
            }

            MqttSessionClientOptions sessionClientOptions = new MqttSessionClientOptions()
            {
            // This retry policy prevents the client from retrying forever
                ConnectionRetryPolicy = new ExponentialBackoffRetryPolicy(10, TimeSpan.FromSeconds(5))
            };

            var sessionClient = new MqttSessionClient(sessionClientOptions);
            await sessionClient.ConnectAsync(new MqttClientOptions(mcs));
            return sessionClient;
        }
    }
}
