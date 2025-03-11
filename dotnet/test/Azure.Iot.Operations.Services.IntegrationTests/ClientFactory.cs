// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Retry;
using System.Diagnostics;

namespace Azure.Iot.Operations.Services.IntegrationTest;

public class ClientFactory
{
    public static async Task<MqttSessionClient> CreateAndConnectClientAsyncFromEnvAsync()
    {
        Debug.Assert(Environment.GetEnvironmentVariable("MQTT_TEST_BROKER_CS") != null);
        string cs = Environment.GetEnvironmentVariable("MQTT_TEST_BROKER_CS")!;
        MqttConnectionSettings mcs = MqttConnectionSettings.FromConnectionString(cs);
        mcs.ClientId += Guid.NewGuid();
        MqttSessionClientOptions sessionClientOptions = new MqttSessionClientOptions()
        {
            // This retry policy prevents the client from retrying forever
            ConnectionRetryPolicy = new ExponentialBackoffRetryPolicy(10, TimeSpan.FromSeconds(5)),
            RetryOnFirstConnect = true, // This helps counteract if MQ is still deploying when the test is run
            EnableMqttLogging = true,
        };

        MqttSessionClient mqttSessionClient = new(sessionClientOptions);
        await mqttSessionClient.ConnectAsync(mcs);
        return mqttSessionClient;
    }

}
