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
            await using CustomTopicTokenClient client = new(new(), mqttClient1);

            await client.StartAsync();

            string expectedTelemetryTopicTokenValue = Guid.NewGuid().ToString();
            Dictionary<string, string> customTopicTokens = new()
            {
                ["ex:myCustomTopicToken"] = expectedTelemetryTopicTokenValue
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
                ["ex:myCustomTopicToken"] = expectedTelemetryTopicTokenValue
            };
            await using CustomTopicTokenService service = new(new(), mqttClient1, customTopicTokens);
            await using CustomTopicTokenClient client = new(new(), mqttClient1);

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
            await using CustomTopicTokenClient client = new(new(), mqttClient1);

            await service.StartAsync();

            string expectedRpcTopicTokenValue = Guid.NewGuid().ToString();
            Dictionary<string, string> customTopicTokens = new()
            {
                ["ex:myCustomTopicToken"] = expectedRpcTopicTokenValue,
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
                ["ex:myCustomTopicToken"] = expectedRpcTopicTokenValue,
            };
            await using CustomTopicTokenService service = new(new(), mqttClient1);
            await using CustomTopicTokenClient client = new(new(), mqttClient1, customTopicTokens);

            await service.StartAsync();

            var result = await client.ReadCustomTopicTokenAsync(mqttClient1.ClientId!, new());

            Assert.Equal(expectedRpcTopicTokenValue, service.ReceivedRpcCustomTopicTokenValue);
            Assert.Equal(expectedRpcTopicTokenValue, result.CustomTopicTokenResponse);
        }

        [Fact]
        public async Task RpcExecutorCanSubscribeToSpecificCustomTopicTokensSetAtStartTime()
        {
            await using MqttSessionClient mqttClient1 = await ClientFactory.CreateSessionClientFromEnvAsync();
            await using MqttSessionClient mqttClient2 = await ClientFactory.CreateSessionClientFromEnvAsync();

            await using CustomTopicTokenService service = new(new(), mqttClient1);
            await using CustomTopicTokenClient client = new(new(), mqttClient1);

            string expectedRpcTopicTokenValue = Guid.NewGuid().ToString();
            Dictionary<string, string> customTopicTokens = new()
            {
                ["ex:myCustomTopicToken"] = expectedRpcTopicTokenValue,
            };

            await service.StartAsync(customTopicTokens);

            var result = await client.ReadCustomTopicTokenAsync(mqttClient1.ClientId!, new(), customTopicTokens);

            Assert.Equal(expectedRpcTopicTokenValue, service.ReceivedRpcCustomTopicTokenValue);
            Assert.Equal(expectedRpcTopicTokenValue, result.CustomTopicTokenResponse);

            Dictionary<string, string> otherCustomTopicTokens = new()
            {
                ["ex:myCustomTopicToken"] = "some new value that shouldn't be handled by executor",
            };
            await Assert.ThrowsAsync<InvalidTimeZoneException>(async () => await client.ReadCustomTopicTokenAsync(mqttClient1.ClientId!, new(), otherCustomTopicTokens));
        }

        [Fact]
        public async Task RpcExecutorCanSubscribeToSpecificCustomTopicTokensSetAtConstructorTime()
        {
            await using MqttSessionClient mqttClient1 = await ClientFactory.CreateSessionClientFromEnvAsync();
            await using MqttSessionClient mqttClient2 = await ClientFactory.CreateSessionClientFromEnvAsync();

            string expectedRpcTopicTokenValue = Guid.NewGuid().ToString();
            Dictionary<string, string> customTopicTokens = new()
            {
                ["ex:myCustomTopicToken"] = expectedRpcTopicTokenValue,
            };

            await using CustomTopicTokenService service = new(new(), mqttClient1, customTopicTokens);
            await using CustomTopicTokenClient client = new(new(), mqttClient1);

            await service.StartAsync();

            var result = await client.ReadCustomTopicTokenAsync(mqttClient1.ClientId!, new(), customTopicTokens);

            Assert.Equal(expectedRpcTopicTokenValue, service.ReceivedRpcCustomTopicTokenValue);
            Assert.Equal(expectedRpcTopicTokenValue, result.CustomTopicTokenResponse);

            Dictionary<string, string> otherCustomTopicTokens = new()
            {
                ["ex:myCustomTopicToken"] = "some new value that shouldn't be handled by executor",
            };
            await Assert.ThrowsAsync<InvalidTimeZoneException>(async () => await client.ReadCustomTopicTokenAsync(mqttClient1.ClientId!, new(), otherCustomTopicTokens));
        }
    }
}
