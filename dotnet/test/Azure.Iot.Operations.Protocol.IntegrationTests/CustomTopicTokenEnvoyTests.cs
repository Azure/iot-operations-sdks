using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Iot.Operations.Mqtt.Session;

namespace Azure.Iot.Operations.Protocol.IntegrationTests
{
    public class CustomTopicTokenEnvoyTests
    {
        [Fact]
        public async Task CanPublishTelemetryWithCustomTopicToken()
        {
            await using MqttSessionClient mqttClient1 = await ClientFactory.CreateSessionClientFromEnvAsync();
            await using MqttSessionClient mqttClient2 = await ClientFactory.CreateSessionClientFromEnvAsync();

            await using CustomTopicTokenService service = new(new(), mqttClient1);
            await using CustomTopicTokenClient client = new(new(), mqttClient1);

            await client.StartAsync();

            string expectedTelemetryTopicTokenValue = Guid.NewGuid().ToString();
            Dictionary<string, string> customTopicTokens = new()
            {
                ["myCustomTopicToken"] = expectedTelemetryTopicTokenValue
            };
            await service.SendTelemetryAsync(new(), new(), customTopicTokens);

            await client.OnTelemetryReceived.Task;

            Assert.Equal(expectedTelemetryTopicTokenValue, client.CustomTopicTokenValue);
        }

        [Fact]
        public async Task CanPublishRpcWithCustomTopicToken()
        {
            await using MqttSessionClient mqttClient1 = await ClientFactory.CreateSessionClientFromEnvAsync();
            await using MqttSessionClient mqttClient2 = await ClientFactory.CreateSessionClientFromEnvAsync();

            await using CustomTopicTokenService service = new(new(), mqttClient1);
            await using CustomTopicTokenClient client = new(new(), mqttClient1);

            await service.StartAsync();

            string expectedRpcTopicTokenValue = Guid.NewGuid().ToString();
            Dictionary<string, string> customTopicTokens = new()
            {
                ["myCustomTopicToken"] = expectedRpcTopicTokenValue,
            };
            var result = await client.ReadCustomTopicTokenAsync(mqttClient1.ClientId!, new(), customTopicTokens);

            Assert.Equal(expectedRpcTopicTokenValue, service.ReceivedRpcCustomTopicTokenValue);
            Assert.Equal(expectedRpcTopicTokenValue, result.CustomTopicTokenResponse);
        }
    }
}
