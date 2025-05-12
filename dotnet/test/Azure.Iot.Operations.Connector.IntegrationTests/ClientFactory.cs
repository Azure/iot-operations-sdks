// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Mqtt;
using Azure.Iot.Operations.Mqtt.Session;
using System.Diagnostics;

namespace Azure.Iot.Operations.Connector.IntegrationTests
{
    public class ClientFactory
    {
        public static async Task<OrderedAckMqttClient> CreateSessionClientFromEnvAsync(CancellationToken cancellationToken = default)
        {
            Debug.Assert(Environment.GetEnvironmentVariable("MQTT_TEST_BROKER_CS") != null);
            string cs = $"{Environment.GetEnvironmentVariable("MQTT_TEST_BROKER_CS")}";
            MqttConnectionSettings mcs = MqttConnectionSettings.FromConnectionString(cs);
            mcs.ClientId += Guid.NewGuid();

            var sessionClient = new MqttSessionClient();

            await sessionClient.ConnectAsync(new MqttClientOptions(mcs), cancellationToken);

            return sessionClient;
        }
    }
}
