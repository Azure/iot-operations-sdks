// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;

namespace Azure.Iot.Operations.Protocol.IntegrationTests
{
    public class CustomTopicTokenEnvoyTests
    {
        [Fact]
        public async Task CanPublishTelemetryWhenCustomTopicTokenSetInPublishCall()
        {
            await using MqttSessionClient mqttClient1 = await ClientFactory.CreateSessionClientFromEnvAsync();
            await using MqttSessionClient mqttClient2 = await ClientFactory.CreateSessionClientFromEnvAsync();

            await using CustomTopicTokenService service = new(new(), mqttClient1);
            await using CustomTopicTokenClient client = new(new(), mqttClient2);

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
        public async Task CanPublishTelemetryWhenCustomTopicTokenSetInConstructor()
        {
            await using MqttSessionClient mqttClient1 = await ClientFactory.CreateSessionClientFromEnvAsync();
            await using MqttSessionClient mqttClient2 = await ClientFactory.CreateSessionClientFromEnvAsync();

            string expectedTelemetryTopicTokenValue = Guid.NewGuid().ToString();
            Dictionary<string, string> customTopicTokens = new()
            {
                ["myCustomTopicToken"] = expectedTelemetryTopicTokenValue
            };
            await using CustomTopicTokenService service = new(new(), mqttClient1, customTopicTokens);
            await using CustomTopicTokenClient client = new(new(), mqttClient2);

            await client.StartAsync();

            await service.SendTelemetryAsync(new(), new());

            await client.OnTelemetryReceived.Task;

            Assert.Equal(expectedTelemetryTopicTokenValue, client.CustomTopicTokenValue);
        }

        [Fact]
        public async Task CanPublishRpcWhenCustomTopicTokenIsSetInInvokeCall()
        {
            await using MqttSessionClient mqttClient1 = await ClientFactory.CreateSessionClientFromEnvAsync();
            await using MqttSessionClient mqttClient2 = await ClientFactory.CreateSessionClientFromEnvAsync();

            await using CustomTopicTokenService service = new(new(), mqttClient1);
            await using CustomTopicTokenClient client = new(new(), mqttClient2);

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

        [Fact]
        public async Task CanPublishRpcWhenCustomTopicTokenIsSetInConstructor()
        {
            await using MqttSessionClient mqttClient1 = await ClientFactory.CreateSessionClientFromEnvAsync();
            await using MqttSessionClient mqttClient2 = await ClientFactory.CreateSessionClientFromEnvAsync();

            string expectedRpcTopicTokenValue = Guid.NewGuid().ToString();
            Dictionary<string, string> customTopicTokens = new()
            {
                ["myCustomTopicToken"] = expectedRpcTopicTokenValue,
            };
            await using CustomTopicTokenService service = new(new(), mqttClient1);
            await using CustomTopicTokenClient client = new(new(), mqttClient2, customTopicTokens);

            await service.StartAsync();

            var result = await client.ReadCustomTopicTokenAsync(mqttClient1.ClientId!, new());

            Assert.Equal(expectedRpcTopicTokenValue, service.ReceivedRpcCustomTopicTokenValue);
            Assert.Equal(expectedRpcTopicTokenValue, result.CustomTopicTokenResponse);
        }

        [Fact]
        public async Task RpcExecutorCanSubscribeToSpecificCustomTopicTokensSetAtConstructorTime()
        {
            await using MqttSessionClient mqttClient1 = await ClientFactory.CreateSessionClientFromEnvAsync();
            await using MqttSessionClient mqttClient2 = await ClientFactory.CreateSessionClientFromEnvAsync();

            string expectedRpcTopicTokenValue = Guid.NewGuid().ToString();
            Dictionary<string, string> customTopicTokens = new()
            {
                ["myCustomTopicToken"] = expectedRpcTopicTokenValue,
            };

            await using CustomTopicTokenService service = new(new(), mqttClient1, customTopicTokens);
            await using CustomTopicTokenClient client = new(new(), mqttClient2);

            await service.StartAsync();

            var result = await client.ReadCustomTopicTokenAsync(mqttClient1.ClientId!, new(), customTopicTokens);

            Assert.Equal(expectedRpcTopicTokenValue, service.ReceivedRpcCustomTopicTokenValue);
            Assert.Equal(expectedRpcTopicTokenValue, result.CustomTopicTokenResponse);

            Dictionary<string, string> otherCustomTopicTokens = new()
            {
                ["myCustomTopicToken"] = "someNewValueThatShouldNotBeHandledByExecutor",
            };

            // This RPC call should fail because the executor isn't listening for invocations with the above topic token value
            var error = await Assert.ThrowsAsync<AkriMqttException>(async () => await client.ReadCustomTopicTokenAsync(mqttClient1.ClientId!, new(), otherCustomTopicTokens, TimeSpan.FromSeconds(3)));
        }
    }
}
