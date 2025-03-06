// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Telemetry;
using Azure.Iot.Operations.Protocol.UnitTests.Serializers.JSON;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.UnitTests.TestSerializers;
using System.Runtime.Serialization;

namespace Azure.Iot.Operations.Protocol.UnitTests;

public class StringTelemetrySender(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
    : TelemetrySender<string>(applicationContext, mqttClient, new Utf8JsonSerializer())
{ }

public class FaultyTelemetrySender(ApplicationContext applicationContext, IMqttPubSubClient mqttClient) : TelemetrySender<string>(applicationContext, mqttClient, new FaultySerializer()) { }


public class TelemetrySenderWithCE(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
    : TelemetrySender<string>(applicationContext, mqttClient, new Utf8JsonSerializer())
{ }

public class TelemetrySenderTests
{
    [Fact]
    public async Task SendTelemetry_FailsWithWrongMqttVersion()
    {
        MockMqttPubSubClient mockClient = new("clientId", MqttProtocolVersion.V310);
        StringTelemetrySender sender = new(new ApplicationContext(), mockClient)
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
        FaultyTelemetrySender sender = new(new ApplicationContext(), mockClient)
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
    public async Task SendTelemetry_PubAckDropped()
    {
        MockMqttPubSubClient mockClient = new();
        StringTelemetrySender sender = new(new ApplicationContext(), mockClient)
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

    [Fact]
    public async Task SendTelemetry_ChecksCancellationToken()
    {
        MockMqttPubSubClient mockClient = new();
        StringTelemetrySender sender = new(new ApplicationContext(), mockClient)
        {
            TopicPattern = "someTopicPattern"
        };

        CancellationTokenSource cts = new();
        cts.Cancel();
        string telemetry = "someTelemetry";
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await sender.SendTelemetryAsync(telemetry, cancellationToken: cts.Token));
    }
}
