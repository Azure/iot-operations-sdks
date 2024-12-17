﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Telemetry;
using Azure.Iot.Operations.Protocol.UnitTests.Serializers.JSON;
using Azure.Iot.Operations.Protocol.UnitTests.Serializers.raw;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.UnitTests.TestSerializers;
using System.Runtime.Serialization;

namespace Azure.Iot.Operations.Protocol.UnitTests;

public class StringTelemetrySender(IMqttPubSubClient mqttClient)
    : TelemetrySender<string>(mqttClient, "test", new Utf8JsonSerializer())
{ }

public class RawTelemetrySender(IMqttPubSubClient mqttClient)
    : TelemetrySender<byte[]>(mqttClient, "test", new PassthroughSerializer())
{ }

public class FaultyTelemetrySender(IMqttPubSubClient mqttClient) : TelemetrySender<string>(mqttClient, "test", new FaultySerializer()) { }

public class TelemetrySenderTests
{
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
    public async Task SendTelemetry_JsonWithContentTypeThrowsException()
    {
        MockMqttPubSubClient mockClient = new();
        StringTelemetrySender sender = new(mockClient)
        {
            TopicPattern = "someTopicPattern"
        };

        string telemetry = "someTelemetry";
        Task sendTelemetry = sender.SendTelemetryAsync(telemetry, contentType: "text/csv");

        AkriMqttException ex = await Assert.ThrowsAsync<AkriMqttException>(() => sendTelemetry);
        Assert.Equal(AkriMqttErrorKind.ArgumentInvalid, ex.Kind);
        Assert.False(ex.InApplication);
        Assert.True(ex.IsShallow);
        Assert.False(ex.IsRemote);
        Assert.Equal("contentType", ex.PropertyName);
        Assert.Equal("text/csv", ex.PropertyValue);
    }

    [Fact]
    public async Task SendTelemetry_RawWithContentTypeSetsContentType()
    {
        MockMqttPubSubClient mockClient = new();
        RawTelemetrySender sender = new(mockClient)
        {
            TopicPattern = "someTopicPattern"
        };

        byte[] telemetry = new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55 };

        await sender.SendTelemetryAsync(telemetry, contentType: "text/csv");
        Assert.Equal("text/csv", mockClient.GetPublishedContentType());
    }
}
