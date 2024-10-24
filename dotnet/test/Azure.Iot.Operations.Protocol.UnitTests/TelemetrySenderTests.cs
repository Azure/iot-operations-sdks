using Azure.Iot.Operations.Protocol.Telemetry;
using Azure.Iot.Operations.Protocol.UnitTests.Serializers.JSON;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.UnitTests.TestSerializers;
using System.Runtime.Serialization;

namespace Azure.Iot.Operations.Protocol.UnitTests;

public class StringTelemetrySender(IMqttPubSubClient mqttClient)
    : TelemetrySender<string>(mqttClient, "test", new Utf8JsonSerializer())
{ }

public class FaultyTelemetrySender(IMqttPubSubClient mqttClient) : TelemetrySender<string>(mqttClient, "test", new FaultySerializer()) { }


public class TelemetrySenderWithCE(IMqttPubSubClient mqttClient)
    : TelemetrySender<string>(mqttClient, "test", new Utf8JsonSerializer())
{ }

public class TelemetrySenderTests
{
    [Fact]
    public async Task SendTelemetry_SingleMessage()
    {
        MockMqttPubSubClient mockClient = new();
        StringTelemetrySender sender = new(mockClient)
        {
            TopicPattern = "someTopicPattern"
        };

        string telemetry = "someTelemetry";
        Task sendTelemetry = sender.SendTelemetryAsync(telemetry);

        await sendTelemetry;
        Assert.Equal(1, mockClient.GetNumberOfPublishes());
    }

    [Fact]
    public async Task SendTelemetry_SingleMessageWithMetadata()
    {
        MockMqttPubSubClient mockClient = new();
        StringTelemetrySender sender = new(mockClient)
        {
            TopicPattern = "someTopicPattern"
        };

        string telemetry = "someTelemetry";
        OutgoingTelemetryMetadata metadata = new();
        HybridLogicalClock expectedTimestamp = metadata.Timestamp;
        string expectedKey = Guid.NewGuid().ToString();
        string expectedValue = Guid.NewGuid().ToString();
        metadata.UserData.Add(expectedKey, expectedValue);

        Task sendTelemetry = sender.SendTelemetryAsync(telemetry, metadata: metadata);

        await sendTelemetry;
        Assert.Equal(1, mockClient.GetNumberOfPublishes());
        Assert.Equal(3, mockClient.MessagesPublished[0].UserProperties!.Count);
        Assert.True(mockClient.MessagesPublished[0].UserProperties!.TryGetProperty(expectedKey, out string? actualValue));
        Assert.Equal(expectedValue, actualValue);
        Assert.True(mockClient.MessagesPublished[0].UserProperties!.TryGetProperty(AkriSystemProperties.Timestamp, out string? actualTimestamp));
        Assert.NotNull(expectedTimestamp);
        Assert.Equal(expectedTimestamp.EncodeToString(), actualTimestamp);
    }

    [Fact]
    public async Task SendTelemetry_MultipleMessages()
    {
        MockMqttPubSubClient mockClient = new();
        StringTelemetrySender sender = new(mockClient)
        {
            TopicPattern = "someTopicPattern"
        };

        string telemetry = "someTelemetry";
        int i;
        for (i = 0; i < 5; i++)
        {
            await sender.SendTelemetryAsync($"{telemetry}{i}");
        }

        Assert.Equal(i, mockClient.GetNumberOfPublishes());
    }

    [Fact]
    public async Task SendTelemetry_EmptyTopicPatternThrows()
    {
        MockMqttPubSubClient mockClient = new();
        StringTelemetrySender sender = new(mockClient);

        string telemetry = "someTelemetry";
        Task sendTelemetry = sender.SendTelemetryAsync(telemetry);

        AkriMqttException ex = await Assert.ThrowsAsync<AkriMqttException>(() => sendTelemetry);
        Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, ex.Kind);
        Assert.False(ex.InApplication);
        Assert.True(ex.IsShallow);
        Assert.False(ex.IsRemote);
        Assert.Null(ex.HttpStatusCode);
        Assert.Equal("TopicPattern", ex.PropertyName);
        Assert.Equal(string.Empty, ex.PropertyValue);
        Assert.Null(ex.CorrelationId);
    }

    [Fact]
    public async Task SendTelemetry_FailsWithWrongMqttVersion()
    {
        MockMqttPubSubClient mockClient = new("clientId", MqttProtocolVersion.V310);
        StringTelemetrySender sender = new(mockClient)
        {
            TopicPattern = "someTopicPattern"
        };

        string telemetry = "someTelemetry";
        Task sendTelemetry = sender.SendTelemetryAsync(telemetry);

        AkriMqttException ex = await Assert.ThrowsAsync<AkriMqttException>(() => sendTelemetry);
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
    public async Task SendTelemetry_MalformedPayloadThrowsException()
    {
        MockMqttPubSubClient mockClient = new();
        FaultyTelemetrySender sender = new(mockClient)
        {
            TopicPattern = "someTopicPattern"
        };

        Task sendTelemetry = sender.SendTelemetryAsync("\\test");

        AkriMqttException ex = await Assert.ThrowsAsync<AkriMqttException>(() => sendTelemetry);
        Assert.Equal(AkriMqttErrorKind.PayloadInvalid, ex.Kind);
        Assert.False(ex.InApplication);
        Assert.True(ex.IsShallow);
        Assert.False(ex.IsRemote);
        Assert.Null(ex.HttpStatusCode);
        Assert.Null(ex.CorrelationId);
        Assert.True(ex.InnerException is SerializationException);
    }

    [Fact]
    public async Task SendTelemetry_InvalidTopicNamespaceThrows()
    {
        MockMqttPubSubClient mockClient = new();
        StringTelemetrySender sender = new(mockClient)
        {
            TopicPattern = "someTopicPattern",
            TopicNamespace = "/sample",
        };

        string telemetry = "someTelemetry";
        Task sendTelemetry = sender.SendTelemetryAsync(telemetry);

        AkriMqttException ex = await Assert.ThrowsAsync<AkriMqttException>(() => sendTelemetry);
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
    public async Task SendTelemetry_PubAckDropped()
    {
        MockMqttPubSubClient mockClient = new();
        StringTelemetrySender sender = new(mockClient)
        {
            TopicPattern = "someTopicPattern/dropPubAck"
        };

        string telemetry = "someTelemetry";
        Task sendTelemetry = sender.SendTelemetryAsync(telemetry);

        AkriMqttException ex = await Assert.ThrowsAsync<AkriMqttException>(() => sendTelemetry);
        Assert.Equal(AkriMqttErrorKind.Timeout, ex.Kind);
        Assert.False(ex.InApplication);
        Assert.False(ex.IsShallow);
        Assert.False(ex.IsRemote);
        Assert.True(ex.InnerException is Exception);

        string expectedExMessage = "Sending telemetry failed due to a MQTT communication error: PubAck dropped.";
        Assert.Equal(expectedExMessage, ex.Message);
    }

    [Theory]
    [InlineData(3)]
    public async Task SendTelemetry_MultipleIdenticalMessages(int numberOfRequests)
    {
        MockMqttPubSubClient mockClient = new();
        StringTelemetrySender sender = new(mockClient)
        {
            TopicPattern = "someTopicPattern"
        };

        for (int i = 0; i < numberOfRequests; i++)
        {
            string telemetry = "someTelemetry";
            Task sendTelemetry = sender.SendTelemetryAsync(telemetry);
            await sendTelemetry;
        }

        Assert.Equal(numberOfRequests, mockClient.GetNumberOfPublishes());
    }

    [Theory]
    [InlineData(3)]
    public async Task SendTelemetry_MultipleUniqueMessages(int numberOfRequests)
    {
        MockMqttPubSubClient mockClient = new();
        StringTelemetrySender sender = new(mockClient)
        {
            TopicPattern = "someTopicPattern"
        };

        for (int i = 0; i < numberOfRequests; i++)
        {
            string telemetry = $"someTelemetry {i}";
            Task sendTelemetry = sender.SendTelemetryAsync(telemetry);
            await sendTelemetry;
        }

        Assert.Equal(numberOfRequests, mockClient.GetNumberOfPublishes());
    }

    [Fact]
    public async Task SendTelemetry_PubAckFailedWithUnspecifiedError()
    {
        MockMqttPubSubClient mockClient = new();
        string topic = "someTopicPattern/failPubAck";
        StringTelemetrySender sender = new(mockClient)
        {
            TopicPattern = topic
        };

        Task sendTelemetry = sender.SendTelemetryAsync("someTelemetry");

        AkriMqttException ex = await Assert.ThrowsAsync<AkriMqttException>(() => sendTelemetry);
        Assert.Equal(AkriMqttErrorKind.MqttError, ex.Kind);
        Assert.False(ex.InApplication);
        Assert.False(ex.IsShallow);
        Assert.False(ex.IsRemote);
        Assert.Null(ex.HttpStatusCode);

        string expectedExMessage = $"Telemetry sending to the topic '{topic}' failed due to an unsuccessful publishing with the error code {MqttClientPublishReasonCode.UnspecifiedError}";
        Assert.Equal(expectedExMessage, ex.Message);
    }

    [Fact]
    public async Task SendTelemetry_PubAckFailedWithNotAuthorized()
    {
        MockMqttPubSubClient mockClient = new();
        string topic = "someTopicPattern/failPubAck/notAuthorized";
        StringTelemetrySender sender = new(mockClient)
        {
            TopicPattern = topic
        };

        Task sendTelemetry = sender.SendTelemetryAsync("someTelemetry");

        AkriMqttException ex = await Assert.ThrowsAsync<AkriMqttException>(() => sendTelemetry);
        Assert.Equal(AkriMqttErrorKind.MqttError, ex.Kind);
        Assert.False(ex.InApplication);
        Assert.False(ex.IsShallow);
        Assert.False(ex.IsRemote);
        Assert.Null(ex.HttpStatusCode);

        string expectedExMessage = $"Telemetry sending to the topic '{topic}' failed due to an unsuccessful publishing with the error code {MqttClientPublishReasonCode.NotAuthorized}";
        Assert.Equal(expectedExMessage, ex.Message);
    }

    [Fact]
    public async Task SendTelemetry_PubAckFailedWithNoMatchingSubscribers()
    {
        MockMqttPubSubClient mockClient = new();
        string topic = "someTopicPattern/failPubAck/noMatchingSubscribers";
        StringTelemetrySender sender = new(mockClient)
        {
            TopicPattern = topic
        };

        Task sendTelemetry = sender.SendTelemetryAsync("someTelemetry");

        AkriMqttException ex = await Assert.ThrowsAsync<AkriMqttException>(() => sendTelemetry);
        Assert.Equal(AkriMqttErrorKind.MqttError, ex.Kind);
        Assert.False(ex.InApplication);
        Assert.False(ex.IsShallow);
        Assert.False(ex.IsRemote);
        Assert.Null(ex.HttpStatusCode);

        string expectedExMessage = $"Telemetry sending to the topic '{topic}' failed due to an unsuccessful publishing with the error code {MqttClientPublishReasonCode.NoMatchingSubscribers}";
        Assert.Equal(expectedExMessage, ex.Message);
    }

    [Fact]
    public async Task SendTelemetry_ChecksCancellationToken()
    {
        MockMqttPubSubClient mockClient = new();
        StringTelemetrySender sender = new(mockClient)
        {
            TopicPattern = "someTopicPattern"
        };

        CancellationTokenSource cts = new();
        cts.Cancel();
        string telemetry = "someTelemetry";
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await sender.SendTelemetryAsync(telemetry, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task SendTelemetry_PropagatesTelemetryTimeout()
    {
        MockMqttPubSubClient mockClient = new();
        StringTelemetrySender sender = new(mockClient)
        {
            TopicPattern = "someTopicPattern"
        };

        TimeSpan telemetryTimeout = TimeSpan.FromSeconds(3);

        string telemetry = "someTelemetry";
        await sender.SendTelemetryAsync(telemetry, telemetryTimeout: telemetryTimeout);

        Assert.Single(mockClient.MessagesPublished);
        Assert.Equal(telemetryTimeout.TotalSeconds, mockClient.MessagesPublished.First().MessageExpiryInterval);
    }

    void AssertUserProperty(List<MqttUserProperty> props, string name, string value)
    {
        if (props.TryGetProperty(name, out var foundValue))
        {
            Assert.Equal(value, foundValue);
        }
        else
        {
            Assert.Fail($"{name} not found in UserProperties");
        }
    }

    [Fact]
    public async Task SendTelemetry_WithoutCloudEvents()
    {
        MockMqttPubSubClient mockClient = new();
        TelemetrySenderWithCE sender = new(mockClient)
        {
            TopicPattern = "someTopicPattern"
        };

        string telemetry = "someTelemetry";

        await sender.SendTelemetryAsync(telemetry, null!, MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan.FromSeconds(10));

        Assert.Equal(1, mockClient.GetNumberOfPublishes());
        var msg = mockClient.MessagesPublished[0];
        Assert.NotNull(msg);
        Assert.NotNull(msg.UserProperties);
        Assert.Single(msg.UserProperties);
        AssertUserProperty(msg.UserProperties, "__protVer", "1.0");
    }

    [Fact]
    public async Task SendTelemetry_With_Null_CloudEvents()
    {
        MockMqttPubSubClient mockClient = new();
        TelemetrySenderWithCE sender = new(mockClient)
        {
            TopicPattern = "someTopicPattern",
        };

        string telemetry = "someTelemetry";

        await sender.SendTelemetryAsync(telemetry, null!, MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan.FromSeconds(10));

        Assert.Equal(1, mockClient.GetNumberOfPublishes());
        var msg = mockClient.MessagesPublished[0];
        Assert.NotNull(msg);
        Assert.NotNull(msg.UserProperties);
        Assert.Single(msg.UserProperties);
        AssertUserProperty(msg.UserProperties, "__protVer", "1.0");
    }


    [Fact]
    public async Task SendTelemetry_WithCloudEvents()
    {
        MockMqttPubSubClient mockClient = new("mock-client");
        TelemetrySenderWithCE sender = new(mockClient)
        {
            TopicPattern = "someTopicPattern/{senderId}/telemetry",
        };

        sender.TopicTokenMap["senderId"] = "mock-client";

        var telemetryMetadata = new OutgoingTelemetryMetadata
        {
            CloudEvent = new CloudEvent(new Uri("mySource", UriKind.RelativeOrAbsolute), "test-type")
            {
                DataSchema = "mystring",
            }
        };

        string telemetry = "someTelemetry";

        await sender.SendTelemetryAsync(telemetry, telemetryMetadata, MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan.FromSeconds(10));

        Assert.Equal(1, mockClient.GetNumberOfPublishes());
        var msg = mockClient.MessagesPublished[0];
        Assert.NotNull(msg);
        Assert.Equal(10, msg.UserProperties!.Count);
        AssertUserProperty(msg.UserProperties, "specversion", "1.0");
        AssertUserProperty(msg.UserProperties, "type", "test-type");
        AssertUserProperty(msg.UserProperties, "datacontenttype", "application/json");
        AssertUserProperty(msg.UserProperties, "dataschema", "mystring");
        AssertUserProperty(msg.UserProperties, "subject", "someTopicPattern/mock-client/telemetry");
        AssertUserProperty(msg.UserProperties, "source", "mySource");

        msg.UserProperties.TryGetProperty("time", out var time1);
        Assert.NotNull(time1);

        msg.UserProperties.TryGetProperty("id", out var id1);
        Assert.NotNull(id1);

        await sender.SendTelemetryAsync(telemetry, telemetryMetadata, MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan.FromSeconds(10));
        var msg2 = mockClient.MessagesPublished[1];
        msg2.UserProperties!.TryGetProperty("time", out var time2);
        Assert.NotNull(time2);
        Assert.NotEqual(time1, time2);

        msg2.UserProperties!.TryGetProperty("id", out var id2);
        Assert.NotNull(id2);
        Assert.NotEqual(id1, id2);
    }

}