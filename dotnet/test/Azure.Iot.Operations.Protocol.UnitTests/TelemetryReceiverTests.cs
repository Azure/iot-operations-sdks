using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.Telemetry;
using Azure.Iot.Operations.Protocol.UnitTests.Serializers.JSON;

namespace Azure.Iot.Operations.Protocol.UnitTests
{
    public class StringTelemetryReceiver : TelemetryReceiver<string>
    {
        public StringTelemetryReceiver(IMqttPubSubClient mqttClient) : base(mqttClient, "test", new Utf8JsonSerializer()) { }
    }

    public class TelemetryReceiverTests
    {
        [Fact]
        public async Task ReceiveTelemetry_FailsWithWrongMqttVersion()
        {
            // Arrange
            var mockClient = new MockMqttPubSubClient("clientId", MqttProtocolVersion.V310);
            var reciever = new StringTelemetryReceiver(mockClient);

            // Act
            Task act() => reciever.StartAsync();

            // Assert
            var ex = await Assert.ThrowsAsync<AkriMqttException>(act);
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
<<<<<<< HEAD
=======
        public async Task ReceiveTelemetry_UnsupportedTopicNamespaceThrows()
        {
            // Arrange
            var mockClient = new MockMqttPubSubClient();
            await using var receiver = new StringTelemetryReceiver(mockClient);

            // Act
            receiver.TopicNamespace = "/sample";

            // Assert
            AkriMqttException ex = await Assert.ThrowsAsync<AkriMqttException>(() => receiver.StartAsync());
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, ex.Kind);
            Assert.False(ex.InApplication);
            Assert.True(ex.IsShallow);
            Assert.False(ex.IsRemote);
            Assert.Null(ex.HttpStatusCode);
            Assert.Equal("TopicNamespace", ex.PropertyName);
            Assert.Equal("/sample", ex.PropertyValue);
            Assert.Null(ex.CorrelationId);
        }

        [Fact]
        public async Task ReceiveTelemetry_InvlidTopicPatternThrows()
        {
            // Arrange
            var mockClient = new MockMqttPubSubClient();
            await using var receiver = new StringTelemetryReceiver(mockClient);

            // Act
            Task act() => receiver.StartAsync();

            // Assert
            var ex = await Assert.ThrowsAsync<AkriMqttException>(act);
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, ex.Kind);
            Assert.False(ex.InApplication);
            Assert.True(ex.IsShallow);
            Assert.False(ex.IsRemote);
            Assert.Null(ex.HttpStatusCode);
            Assert.Equal("TopicPattern", ex.PropertyName);
            Assert.Equal(string.Empty, ex.PropertyValue);
            Assert.Null(ex.CorrelationId);

            await receiver.StopAsync();
        }

        [Fact]
        public async Task ReceiveTelemetry_ReceiverStarts()
        {
            // Arrange
            var mockClient = new MockMqttPubSubClient();
            await using var receiver = new StringTelemetryReceiver(mockClient)
            {
                TopicPattern = "someTopicPattern",
                TopicNamespace = "test",
            };


            // Act
            await receiver.StartAsync();

            // Assert
            Assert.Equal("test/someTopicPattern", mockClient.SubscribedTopicReceived);
            Assert.Equal(MqttQualityOfServiceLevel.AtLeastOnce, mockClient.SubscribedTopicQoSReceived);
            await receiver.StopAsync();
        }

        [Fact]
        public async Task ReceiveTelemetry_MalformedPayloadThrows()
        {
            // Arrange
            var mockClient = new MockMqttPubSubClient();
            int telemetryCount = 0;
            string telemetryReceived = "";
            await using var receiver = new StringTelemetryReceiver(mockClient)
            {
                TopicPattern = "someTopicPattern",
                TopicNamespace = "test",
                OnTelemetryReceived = (string _, string response, IncomingTelemetryMetadata metadata) => {
                    telemetryReceived = response;
                    telemetryCount++;
                    return Task.CompletedTask;
                }
            };

            MqttApplicationMessage message = new MqttApplicationMessage("test/someTopicPattern")
            {
                PayloadSegment = Encoding.UTF8.GetBytes("\\test"),
                ContentType = "application/json",
            };

            // Act
            await receiver.StartAsync();
            await mockClient.SimulateNewMessage(message);
            await mockClient.SimulatedMessageAcknowledged();

            // Assert
            Assert.Equal(0, telemetryCount);
        }

        [Fact]
        public async Task ReceiveTelemetry_NoPayloadWhenExpected()
        {
            // Arrange
            var mockClient = new MockMqttPubSubClient();
            int telemetryCount = 0;
            string telemetryReceived = "";
            await using var receiver = new StringTelemetryReceiver(mockClient)
            {
                TopicPattern = "someTopicPattern",
                TopicNamespace = "test",
                OnTelemetryReceived = (string _, string response, IncomingTelemetryMetadata metadata) =>
                {
                    telemetryReceived = response;
                    telemetryCount++;
                    return Task.CompletedTask;
                },
            };

            var message = new MqttApplicationMessage("test/someTopicPattern")
            {
                ContentType = "application/json"
            };

            // Act
            await receiver.StartAsync();
            await mockClient.SimulateNewMessage(message);
            await mockClient.SimulatedMessageAcknowledged();

            // Assert
            Assert.Equal(0, telemetryCount);

            // Re-act
            var serializer = new Utf8JsonSerializer();
            message.PayloadSegment = serializer.ToBytes("testPayload") ?? Array.Empty<byte>();
            await mockClient.SimulateNewMessage(message);
            await mockClient.SimulatedMessageAcknowledged();
            Assert.Equal(1, telemetryCount);
            await receiver.StopAsync();
        }

        [Fact]
        public async Task ReceiveTelemetry_MismatchedContentTypeDoesNotThrow()
        {
            // Arrange
            var mockClient = new MockMqttPubSubClient();
            int telemetryCount = 0;
            string telemetryReceived = "";
            await using var receiver = new StringTelemetryReceiver(mockClient)
            {
                TopicPattern = "someTopicPattern",
                TopicNamespace = "test",
                OnTelemetryReceived = (string _, string response, IncomingTelemetryMetadata metadata) =>
                {
                    telemetryReceived = response;
                    telemetryCount++;
                    return Task.CompletedTask;
                }
            };

            MqttApplicationMessage message = new MqttApplicationMessage("test/someTopicPattern")
            {
                PayloadSegment = Encoding.UTF8.GetBytes("test"),
                ContentType = "application/protobuf",
            };

            // Act
            await receiver.StartAsync();

            // Assert
            var ex = await Record.ExceptionAsync(async () => await mockClient.SimulateNewMessage(message));
            Assert.Null(ex);
            await receiver.StopAsync();
        }

        [Theory]
        [InlineData(3)]
        [InlineData(10)]
        [InlineData(100)]
        public async Task ReceiveTelemetry_TwoOrMoreIdenticalMessages(int numberOfMessages)
        {
            // Arrange
            var mockClient = new MockMqttPubSubClient();
            var serializer = new Utf8JsonSerializer();
            int telemetryCount = 0;
            string telemetryReceived = "";
            await using var receiver = new StringTelemetryReceiver(mockClient)
            {
                TopicPattern = "someTopicPattern",
                TopicNamespace = "test",
                OnTelemetryReceived = (string _, string response, IncomingTelemetryMetadata metadata) =>
                {
                    telemetryReceived = response;
                    telemetryCount++;
                    return Task.CompletedTask;
                }
            };

            // Act
            string expectedTelemetry = "testTelemetry";
            await receiver.StartAsync();

            for (int i = 0; i < numberOfMessages; i++)
            {
                var message = new MqttApplicationMessage($"{receiver.TopicNamespace}/{receiver.TopicPattern}")
                {
                    PayloadSegment = serializer.ToBytes<string>(expectedTelemetry) ?? Array.Empty<byte>(),
                    PayloadFormatIndicator = (MqttPayloadFormatIndicator)serializer.CharacterDataFormatIndicator,
                };

                await mockClient.SimulateNewMessage(message);
                await mockClient.SimulatedMessageAcknowledged();
            }

            // Assert
            Assert.Equal(expectedTelemetry, telemetryReceived);
            Assert.Equal(numberOfMessages, telemetryCount);
            await receiver.StopAsync();
        }

        [Theory]
        [InlineData(3)]
        [InlineData(10)]
        [InlineData(100)]
        public async Task ReceiveTelemetry_TwoOrMoreUniqueMessages(int numberOfMessages)
        {
            // Arrange
            var mockClient = new MockMqttPubSubClient();
            var serializer = new Utf8JsonSerializer();
            int telemetryCount = 0;
            string telemetryReceived = "";
            await using var receiver = new StringTelemetryReceiver(mockClient)
            {
                TopicPattern = "someTopicPattern",
                TopicNamespace = "test",
                OnTelemetryReceived = (string _, string response, IncomingTelemetryMetadata metadata) =>
                {
                    telemetryReceived = response;
                    telemetryCount++;
                    return Task.CompletedTask;
                }
            };

            // Act
            await receiver.StartAsync();

            for (int i = 0; i < numberOfMessages; i++)
            {
                string expectedTelemetry = $"testTelemetry{i}";

                var message = new MqttApplicationMessage($"{receiver.TopicNamespace}/{receiver.TopicPattern}")
                {
                    PayloadSegment = serializer.ToBytes<string>(expectedTelemetry) ?? Array.Empty<byte>(),
                    PayloadFormatIndicator = (MqttPayloadFormatIndicator)serializer.CharacterDataFormatIndicator
                };

                await mockClient.SimulateNewMessage(message);
                await mockClient.SimulatedMessageAcknowledged();
                Assert.Equal(expectedTelemetry, telemetryReceived);
            }

            // Assert
            Assert.Equal(numberOfMessages, telemetryCount);
            await receiver.StopAsync();
        }

        [Fact]
>>>>>>> main
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
            var receiver1 = new StringTelemetryReceiver(mockClient)
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

            var receiver2 = new StringTelemetryReceiver(mockClient)
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

            var message = new MqttApplicationMessage($"{receiver1.TopicNamespace}/{receiver1.TopicPattern}")
            {
                PayloadSegment = serializer.ToBytes<string>(expectedTelemetry) ?? Array.Empty<byte>(),
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)serializer.CharacterDataFormatIndicator,
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
            await using var receiver = new StringTelemetryReceiver(mockClient)
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
            await using var receiver = new StringTelemetryReceiver(mockClient)
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
