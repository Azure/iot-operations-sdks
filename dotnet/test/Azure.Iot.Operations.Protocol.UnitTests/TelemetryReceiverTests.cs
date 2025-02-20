// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.Telemetry;
using Azure.Iot.Operations.Protocol.UnitTests.Serializers.JSON;

namespace Azure.Iot.Operations.Protocol.UnitTests
{
    public class StringTelemetryReceiver : TelemetryReceiver<string>
    {
        public StringTelemetryReceiver(ApplicationContext applicationContext, IMqttPubSubClient mqttClient) : base(applicationContext, mqttClient, "test", new Utf8JsonSerializer()) { }
    }

    public class TelemetryReceiverTests
    {
        [Fact]
        public async Task ReceiveTelemetry_FailsWithWrongMqttVersion()
        {
            // Arrange
            var mockClient = new MockMqttPubSubClient("clientId", MqttProtocolVersion.V310);
            var reciever = new StringTelemetryReceiver(new ApplicationContext(), mockClient);

            // Act
            Task Act() => reciever.StartAsync();

            // Assert
            var ex = await Assert.ThrowsAsync<AkriMqttException>(Act);
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, ex.Kind);
            Assert.False(ex.InApplication);
            Assert.True(ex.IsShallow);
            Assert.False(ex.IsRemote);
            Assert.Null(ex.HttpStatusCode);
            Assert.Equal("MQTTClient.ProtocolVersion", ex.PropertyName);
            Assert.Equal(MqttProtocolVersion.V310, ex.PropertyValue);
            Assert.Null(ex.CorrelationId);
        }

        [Fact]
        public async Task ReceiveTelemetry_MultipleReceiversSameClient()
        {
            // Arrange
            var mockClient = new MockMqttPubSubClient();
            var serializer = new Utf8JsonSerializer();
            int telemetryCount = 0;
            string telemetryReceived1 = "";
            string telemetryReceived2 = "";
            SemaphoreSlim semaphore1 = new(0);
            SemaphoreSlim semaphore2 = new(0);
            var receiver1 = new StringTelemetryReceiver(new ApplicationContext(), mockClient)
            {
                TopicPattern = "someTopicPattern",
                TopicNamespace = "test",
                OnTelemetryReceived = (string _, string response, IncomingTelemetryMetadata metadata) =>
                {
                    telemetryReceived1 = response;
                    Interlocked.Increment(ref telemetryCount);
                    semaphore1.Release();
                    return Task.CompletedTask;
                }
            };

            var receiver2 = new StringTelemetryReceiver(new ApplicationContext(), mockClient)
            {
                TopicPattern = "someTopicPattern",
                TopicNamespace = "test",
                OnTelemetryReceived = (string _, string response, IncomingTelemetryMetadata metadata) =>
                {
                    telemetryReceived2 = response;
                    Interlocked.Increment(ref telemetryCount);
                    semaphore2.Release();
                    return Task.CompletedTask;
                }
            };

            // Act
            string expectedTelemetry = "testTelemetry";
            await receiver1.StartAsync();
            await receiver2.StartAsync();

            SerializedPayloadContext payloadContext = serializer.ToBytes(expectedTelemetry);
            var message = new MqttApplicationMessage($"{receiver1.TopicNamespace}/{receiver1.TopicPattern}")
            {
                PayloadSegment = payloadContext.SerializedPayload ?? Array.Empty<byte>(),
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)payloadContext.PayloadFormatIndicator,
                ContentType = payloadContext.ContentType,
            };

            await mockClient.SimulateNewMessage(message);
            await mockClient.SimulatedMessageAcknowledged();

            await semaphore1.WaitAsync();
            await semaphore2.WaitAsync();

            // Assert
            Assert.Equal(expectedTelemetry, telemetryReceived1);
            Assert.Equal(expectedTelemetry, telemetryReceived2);
            Assert.Equal(2, telemetryCount);
            await receiver1.StopAsync();
            await receiver2.StopAsync();
        }

        [Fact]
        public async Task ReceiveTelemetry_ThrowsIfAccessedWhenDisposed()
        {
            // Arrange
            var mockClient = new MockMqttPubSubClient();
            var serializer = new Utf8JsonSerializer();
            await using var receiver = new StringTelemetryReceiver(new ApplicationContext(), mockClient)
            {
                TopicPattern = "someTopicPattern",
                TopicNamespace = "test",
                OnTelemetryReceived = (string _, string response, IncomingTelemetryMetadata metadata) =>
                {
                    return Task.CompletedTask;
                }
            };

            await receiver.DisposeAsync();

            // Act
            // Assert
            await Assert.ThrowsAsync<ObjectDisposedException>(() => receiver.StartAsync());
            await Assert.ThrowsAsync<ObjectDisposedException>(() => receiver.StopAsync());
        }

        [Fact]
        public async Task ReceiveTelemetry_ThrowsIfCancellationRequested()
        {
            var mockClient = new MockMqttPubSubClient();
            var serializer = new Utf8JsonSerializer();
            await using var receiver = new StringTelemetryReceiver(new ApplicationContext(), mockClient)
            {
                TopicPattern = "someTopicPattern",
                TopicNamespace = "test",
                OnTelemetryReceived = (string _, string response, IncomingTelemetryMetadata metadata) =>
                {
                    return Task.CompletedTask;
                }
            };

            CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => receiver.StartAsync(cancellationToken: cts.Token));
            await Assert.ThrowsAsync<OperationCanceledException>(() => receiver.StopAsync(cancellationToken: cts.Token));
        }
    }
}
