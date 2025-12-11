// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Protocol.Telemetry;

namespace Azure.Iot.Operations.Protocol.UnitTests;

public class ProtocolCloudEventTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var cloudEvent = new ProtocolCloudEvent(new Uri("test://source"));

        // Assert
        Assert.Equal("test://source/", cloudEvent.Source.ToString());
        Assert.Equal("1.0", cloudEvent.SpecVersion);
        Assert.Equal("ms.aio.telemetry", cloudEvent.Type); // Default type
        Assert.Null(cloudEvent.Id);
        Assert.Null(cloudEvent.Time);
        Assert.Null(cloudEvent.DataContentType);
        Assert.Null(cloudEvent.Subject);
        Assert.Null(cloudEvent.DataSchema);
    }

    [Fact]
    public void SetCloudEvent_ProtocolCloudEvent_SerializesCorrectly()
    {
        // Arrange
        var message = new MqttApplicationMessage("test/topic");
        var cloudEvent = new ProtocolCloudEvent(new Uri("test://source"))
        {
            Id = "proto-id",
            Time = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            Subject = "proto-subject",
            DataSchema = "https://schema.example.com",
            Type = "custom.protocol.type", // Set via internal setter
            DataContentType = "application/protobuf" // Set via internal setter
        };
        message.ContentType = "application/json";
        // Act
        message.SetCloudEvent(cloudEvent);

        // Assert
        Assert.Contains(message.UserProperties!, p => p.Name == "specversion" && p.Value == "1.0");
        Assert.Contains(message.UserProperties!, p => p.Name == "id" && p.Value == "proto-id");
        Assert.Contains(message.UserProperties!, p => p.Name == "source" && p.Value == "test://source/");
        Assert.Contains(message.UserProperties!, p => p.Name == "type" && p.Value == "custom.protocol.type");
        Assert.Contains(message.UserProperties!, p => p.Name == "subject" && p.Value == "proto-subject");
        Assert.Contains(message.UserProperties!, p => p.Name == "dataschema" && p.Value == "https://schema.example.com");
        Assert.Equal("application/json", message.ContentType);
    }

    [Fact]
    public void GetProtocolCloudEvent_WithValidData_ReturnsProtocolCloudEvent()
    {
        // Arrange
        var message = new MqttApplicationMessage("test/topic");
        message.AddUserProperty("specversion", "1.0");
        message.AddUserProperty("source", "test://device");
        message.AddUserProperty("type", "ms.aio.telemetry");
        message.AddUserProperty("id", "event-123");
        message.ContentType = "application/json";

        // Act
        var cloudEvent = message.GetProtocolCloudEvent();

        // Assert
        Assert.NotNull(cloudEvent);
        Assert.Equal("1.0", cloudEvent.SpecVersion);
        Assert.Equal("test://device/", cloudEvent.Source.ToString());
        Assert.Equal("ms.aio.telemetry", cloudEvent.Type);
        Assert.Equal("event-123", cloudEvent.Id);
        Assert.Equal("application/json", cloudEvent.DataContentType);
    }

    [Fact]
    public void GetProtocolCloudEvent_RoundTrip_PreservesProperties()
    {
        // Arrange
        var message = new MqttApplicationMessage("test/topic");
        var original = new ProtocolCloudEvent(new Uri("test://sensor"))
        {
            Id = "round-trip-123",
            Time = new DateTime(2024, 6, 15, 10, 30, 45, DateTimeKind.Utc),
            Subject = "temperature",
            DataSchema = "https://schema.example.com/v3",
            Type = "ms.aio.rpc.request",
            DataContentType = "application/avro"
        };
        message.SetCloudEvent(original);
        message.ContentType = "application/json";
        // Act
        var retrieved = message.GetProtocolCloudEvent();

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(original.SpecVersion, retrieved.SpecVersion);
        Assert.Equal(original.Source.ToString(), retrieved.Source.ToString());
        Assert.Equal(original.Type, retrieved.Type);
        Assert.Equal(original.Id, retrieved.Id);
        Assert.Equal(original.Time, retrieved.Time);
        Assert.Equal(message.ContentType ,retrieved.DataContentType);
        Assert.Equal(original.Subject, retrieved.Subject);
        Assert.Equal(original.DataSchema, retrieved.DataSchema);
    }

    [Fact]
    public void OutgoingTelemetryMetadata_WithProtocolCloudEvent_Works()
    {
        // Arrange & Act
        var metadata = new OutgoingTelemetryMetadata
        {
            CloudEvent = new ProtocolCloudEvent(new Uri("aio://device/sensor1"))
            {
                DataSchema = "sr://namespace/schema#1"
            }
        };

        // Assert
        Assert.NotNull(metadata.CloudEvent);
        Assert.Equal("aio://device/sensor1", metadata.CloudEvent.Source.ToString());
        Assert.Equal("ms.aio.telemetry", metadata.CloudEvent.Type); // Default
        Assert.Equal("sr://namespace/schema#1", metadata.CloudEvent.DataSchema);
    }

    [Fact]
    public void GetProtocolCloudEvent_WithoutRequiredFields_ReturnsNull()
    {
        // Arrange
        var message = new MqttApplicationMessage("test/topic");
        message.AddUserProperty("specversion", "1.0");
        // Missing source and type

        // Act
        var cloudEvent = message.GetProtocolCloudEvent();

        // Assert
        Assert.Null(cloudEvent);
    }

    [Fact]
    public void ProtocolCloudEvent_TypeProperty_CanBeSetInternally()
    {
        // Arrange & Act - Simulate SDK setting Type for different contexts
        var telemetryEvent = new ProtocolCloudEvent(new Uri("aio://device"))
        {
            Type = "ms.aio.telemetry"
        };

        var requestEvent = new ProtocolCloudEvent(new Uri("aio://device"))
        {
            Type = "ms.aio.rpc.request"
        };

        var responseEvent = new ProtocolCloudEvent(new Uri("aio://device"))
        {
            Type = "ms.aio.rpc.response"
        };

        // Assert
        Assert.Equal("ms.aio.telemetry", telemetryEvent.Type);
        Assert.Equal("ms.aio.rpc.request", requestEvent.Type);
        Assert.Equal("ms.aio.rpc.response", responseEvent.Type);
    }

    [Fact]
    public void ProtocolCloudEvent_DataContentTypeProperty_CanBeSetInternally()
    {
        // Arrange & Act - Simulate SDK setting DataContentType from different serializers
        var jsonEvent = new ProtocolCloudEvent(new Uri("aio://device"))
        {
            DataContentType = "application/json"
        };

        var avroEvent = new ProtocolCloudEvent(new Uri("aio://device"))
        {
            DataContentType = "application/avro"
        };

        var protobufEvent = new ProtocolCloudEvent(new Uri("aio://device"))
        {
            DataContentType = "application/protobuf"
        };

        // Assert
        Assert.Equal("application/json", jsonEvent.DataContentType);
        Assert.Equal("application/avro", avroEvent.DataContentType);
        Assert.Equal("application/protobuf", protobufEvent.DataContentType);
    }

    [Fact]
    public void CommandRequestMetadata_WithProtocolCloudEvent_SerializesCorrectly()
    {
        // Arrange
        var metadata = new CommandRequestMetadata
        {
            CloudEvent = new ProtocolCloudEvent(new Uri("aio://command/source"))
            {
                Type = "ms.aio.rpc.request",
                DataContentType = "application/json",
                Subject = "command-subject",
                DataSchema = "sr://namespace/schema#1"
            }
        };
        metadata.UserData["custom-header"] = "custom-value";

        var message = new MqttApplicationMessage("command/request");

        // Act
        metadata.MarshalTo(message);

        // Assert
        Assert.Contains(message.UserProperties!, p => p.Name == "type" && p.Value == "ms.aio.rpc.request");
        Assert.Contains(message.UserProperties!, p => p.Name == "source" && p.Value == "aio://command/source");
        Assert.Contains(message.UserProperties!, p => p.Name == "subject" && p.Value == "command-subject");
        Assert.Contains(message.UserProperties!, p => p.Name == "dataschema" && p.Value == "sr://namespace/schema#1");
        Assert.Contains(message.UserProperties!, p => p.Name == "custom-header" && p.Value == "custom-value");
    }

    [Fact]
    public void CommandRequestMetadata_ParseFromMessage_ReturnsProtocolCloudEvent()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var message = new MqttApplicationMessage("command/request")
        {
            CorrelationData = correlationId.ToByteArray(),
            ContentType = "application/json"
        };
        message.AddUserProperty("specversion", "1.0");
        message.AddUserProperty("source", "aio://client");
        message.AddUserProperty("type", "ms.aio.rpc.request");
        message.AddUserProperty("id", "cmd-456");

        // Act
        var metadata = new CommandRequestMetadata(message, "command/request", null);

        // Assert
        Assert.NotNull(metadata.CloudEvent);
        Assert.IsType<ProtocolCloudEvent>(metadata.CloudEvent);
        Assert.Equal("aio://client/", metadata.CloudEvent.Source.ToString());
        Assert.Equal("ms.aio.rpc.request", metadata.CloudEvent.Type);
        Assert.Equal("cmd-456", metadata.CloudEvent.Id);
        Assert.Equal("application/json", metadata.CloudEvent.DataContentType);
    }

    [Fact]
    public void CommandResponseMetadata_WithProtocolCloudEvent_SerializesCorrectly()
    {
        // Arrange
        var metadata = new CommandResponseMetadata
        {
            CloudEvent = new ProtocolCloudEvent(new Uri("aio://server"))
            {
                Type = "ms.aio.rpc.response",
                DataContentType = "application/avro",
                Subject = "response-subject"
            }
        };
        metadata.UserData["custom-key"] = "custom-value";

        var message = new MqttApplicationMessage("command/response");

        // Act
        metadata.MarshalTo(message);

        // Assert
        Assert.Contains(message.UserProperties!, p => p.Name == "type" && p.Value == "ms.aio.rpc.response");
        Assert.Contains(message.UserProperties!, p => p.Name == "source" && p.Value == new Uri("aio://server").ToString());
        Assert.Contains(message.UserProperties!, p => p.Name == "subject" && p.Value == "response-subject");
        Assert.Contains(message.UserProperties!, p => p.Name == "custom-key" && p.Value == "custom-value");
    }

    [Fact]
    public void CommandResponseMetadata_ParseFromMessage_ReturnsProtocolCloudEvent()
    {
        // Arrange
        var message = new MqttApplicationMessage("response/topic");
        message.AddUserProperty("specversion", "1.0");
        message.AddUserProperty("source", "aio://server");
        message.AddUserProperty("type", "ms.aio.rpc.response");
        message.AddUserProperty("id", "resp-789");
        message.CorrelationData = Guid.NewGuid().ToByteArray();
        message.ContentType = "application/protobuf";

        // Act
        var metadata = new CommandResponseMetadata(message);

        // Assert
        Assert.NotNull(metadata.CloudEvent);
        Assert.IsType<ProtocolCloudEvent>(metadata.CloudEvent);
        Assert.Equal("aio://server/", metadata.CloudEvent.Source.ToString());
        Assert.Equal("ms.aio.rpc.response", metadata.CloudEvent.Type);
        Assert.Equal("resp-789", metadata.CloudEvent.Id);
        Assert.Equal("application/protobuf", metadata.CloudEvent.DataContentType);
    }

    [Fact]
    public void IncomingTelemetryMetadata_GetCloudEvent_ReturnsProtocolCloudEvent()
    {
        // Arrange
        var message = new MqttApplicationMessage("telemetry/sensor1")
        {
            ContentType = "application/json"
        };
        message.AddUserProperty("specversion", "1.0");
        message.AddUserProperty("source", "aio://factory/sensor1");
        message.AddUserProperty("type", "ms.aio.telemetry");
        message.AddUserProperty("id", "telem-001");
        message.AddUserProperty("time", "2024-12-11T15:30:00Z");
        message.AddUserProperty("subject", "temperature");
        message.AddUserProperty("dataschema", "sr://telemetry/temperature#v2");

        var metadata = new IncomingTelemetryMetadata(message, 1, "telemetry/sensor1", null);

        // Act
        var cloudEvent = metadata.GetCloudEvent();

        // Assert
        Assert.NotNull(cloudEvent);
        Assert.IsType<ProtocolCloudEvent>(cloudEvent);
        Assert.Equal("aio://factory/sensor1", cloudEvent.Source.ToString());
        Assert.Equal("ms.aio.telemetry", cloudEvent.Type);
        Assert.Equal("telem-001", cloudEvent.Id);
        Assert.Equal("temperature", cloudEvent.Subject);
        Assert.Equal("sr://telemetry/temperature#v2", cloudEvent.DataSchema);
        Assert.Equal("application/json", cloudEvent.DataContentType);
        Assert.NotNull(cloudEvent.Time);
    }

    [Fact]
    public void IncomingTelemetryMetadata_GetCloudEvent_ThrowsOnMissingType()
    {
        // Arrange
        var message = new MqttApplicationMessage("telemetry/sensor1");
        message.AddUserProperty("specversion", "1.0");
        message.AddUserProperty("source", "aio://sensor");
        message.AddUserProperty("id", "test-id");
        // Missing type property

        var metadata = new IncomingTelemetryMetadata(message, 1, "telemetry/sensor1", null);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => metadata.GetCloudEvent());
        Assert.Contains("Type", exception.Message);
    }

    [Fact]
    public void SetCloudEvent_WithoutIdOrTime_GeneratesDefaults()
    {
        // Arrange
        var message = new MqttApplicationMessage("test/topic");
        var cloudEvent = new ProtocolCloudEvent(new Uri("test://source"))
        {
            Type = "ms.aio.telemetry"
        };
        var before = DateTime.UtcNow;

        // Act
        message.SetCloudEvent(cloudEvent);
        var after = DateTime.UtcNow;

        // Assert - Id is generated
        Assert.NotNull(cloudEvent.Id);
        Assert.True(Guid.TryParse(cloudEvent.Id, out _));

        // Assert - Time is generated
        Assert.NotNull(cloudEvent.Time);
        Assert.True(cloudEvent.Time >= before && cloudEvent.Time <= after);

        // Assert - Values are in message
        Assert.Contains(message.UserProperties!, p => p.Name == "id" && !string.IsNullOrEmpty(p.Value));
        Assert.Contains(message.UserProperties!, p => p.Name == "time");
    }
}
