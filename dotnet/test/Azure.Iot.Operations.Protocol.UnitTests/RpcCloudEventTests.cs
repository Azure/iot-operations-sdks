// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Protocol.Telemetry;

namespace Azure.Iot.Operations.Protocol.Tests.RPC;

public class RpcCloudEventTests
{
    [Fact]
    public void CommandRequestMetadata_WithCloudEvent_MarshalsProperly()
    {
        // Arrange
        var source = new Uri("aio://test/command");
        var cloudEvent = new CloudEvent(source, "ms.aio.rpc.request")
        {
            Id = "test-request-id",
            Subject = "test/request/topic",
            Time = new DateTime(2025, 12, 2, 10, 0, 0, DateTimeKind.Utc)
        };

        var metadata = new CommandRequestMetadata
        {
            CloudEvent = cloudEvent
        };

        var message = new MqttApplicationMessage("test/topic");

        // Act
        metadata.MarshalTo(message);

        // Assert
        Assert.NotNull(message.UserProperties);
        Assert.Contains(message.UserProperties, p => p.Name == "id" && p.Value == "test-request-id");
        Assert.Contains(message.UserProperties, p => p.Name == "source" && p.Value == source.ToString());
        Assert.Contains(message.UserProperties, p => p.Name == "type" && p.Value == "ms.aio.rpc.request");
        Assert.Contains(message.UserProperties, p => p.Name == "specversion" && p.Value == "1.0");
        Assert.Contains(message.UserProperties, p => p.Name == "subject" && p.Value == "test/request/topic");
        Assert.Contains(message.UserProperties, p => p.Name == "time" && p.Value == "2025-12-02T10:00:00Z");
    }

    [Fact]
    public void CommandRequestMetadata_WithoutCloudEvent_DoesNotIncludeCloudEventProperties()
    {
        // Arrange
        var metadata = new CommandRequestMetadata();
        var message = new MqttApplicationMessage("test/topic");

        // Act
        metadata.MarshalTo(message);

        // Assert
        if (message.UserProperties != null)
        {
            Assert.DoesNotContain(message.UserProperties, p => p.Name == "id");
            Assert.DoesNotContain(message.UserProperties, p => p.Name == "source");
            Assert.DoesNotContain(message.UserProperties, p => p.Name == "type");
            Assert.DoesNotContain(message.UserProperties, p => p.Name == "specversion");
        }
    }

    [Fact]
    public void CommandResponseMetadata_WithCloudEvent_MarshalsProperly()
    {
        // Arrange
        var source = new Uri("aio://test/response");
        var cloudEvent = new CloudEvent(source, "ms.aio.rpc.response")
        {
            Id = "test-response-id",
            Subject = "test/response/topic",
            Time = new DateTime(2025, 12, 2, 11, 0, 0, DateTimeKind.Utc)
        };

        var metadata = new CommandResponseMetadata
        {
            CloudEvent = cloudEvent
        };

        var message = new MqttApplicationMessage("test/topic");

        // Act
        metadata.MarshalTo(message);

        // Assert
        Assert.NotNull(message.UserProperties);
        Assert.Contains(message.UserProperties, p => p.Name == "id" && p.Value == "test-response-id");
        Assert.Contains(message.UserProperties, p => p.Name == "source" && p.Value == source.ToString());
        Assert.Contains(message.UserProperties, p => p.Name == "type" && p.Value == "ms.aio.rpc.response");
        Assert.Contains(message.UserProperties, p => p.Name == "specversion" && p.Value == "1.0");
        Assert.Contains(message.UserProperties, p => p.Name == "subject" && p.Value == "test/response/topic");
        Assert.Contains(message.UserProperties, p => p.Name == "time" && p.Value == "2025-12-02T11:00:00Z");
    }

    [Fact]
    public void CommandResponseMetadata_ParseCloudEventFromMessage()
    {
        // Arrange
        var message = new MqttApplicationMessage("test/topic")
        {
            CorrelationData = Guid.NewGuid().ToByteArray(),
            ContentType = "application/json",
            UserProperties = new List<MqttUserProperty>
            {
                new MqttUserProperty("id", "test-response-id"),
                new MqttUserProperty("source", "aio://test/source"),
                new MqttUserProperty("type", "ms.aio.rpc.response"),
                new MqttUserProperty("specversion", "1.0"),
                new MqttUserProperty("subject", "test/response/subject"),
                new MqttUserProperty("time", "2025-12-02T13:00:00Z")
            }
        };

        // Act
        var metadata = new CommandResponseMetadata(message);

        // Assert
        Assert.NotNull(metadata.CloudEvent);
        Assert.Equal("test-response-id", metadata.CloudEvent.Id);
        Assert.Equal("aio://test/source", metadata.CloudEvent.Source.ToString());
        Assert.Equal("ms.aio.rpc.response", metadata.CloudEvent.Type);
        Assert.Equal("1.0", metadata.CloudEvent.SpecVersion);
        Assert.Equal("test/response/subject", metadata.CloudEvent.Subject);
        Assert.Equal("application/json", metadata.CloudEvent.DataContentType);
        Assert.NotNull(metadata.CloudEvent.Time);
    }

    [Fact]
    public void CommandResponseMetadata_WithoutCloudEvent_DoesNotIncludeCloudEventProperties()
    {
        // Arrange
        var metadata = new CommandResponseMetadata();
        var message = new MqttApplicationMessage("test/topic");

        // Act
        metadata.MarshalTo(message);

        // Assert
        if (message.UserProperties != null)
        {
            Assert.DoesNotContain(message.UserProperties, p => p.Name == "id");
            Assert.DoesNotContain(message.UserProperties, p => p.Name == "source");
            Assert.DoesNotContain(message.UserProperties, p => p.Name == "type");
            Assert.DoesNotContain(message.UserProperties, p => p.Name == "specversion");
        }
    }

    [Fact]
    public void CommandRequestMetadata_CloudEventProperty_CanBeSetAndRetrieved()
    {
        // Arrange
        var cloudEvent = new CloudEvent(new Uri("aio://test/command"), "ms.aio.rpc.request")
        {
            Id = "test-id",
            Subject = "test/subject"
        };

        // Act
        var metadata = new CommandRequestMetadata
        {
            CloudEvent = cloudEvent
        };

        // Assert
        Assert.NotNull(metadata.CloudEvent);
        Assert.Equal("test-id", metadata.CloudEvent.Id);
        Assert.Equal("ms.aio.rpc.request", metadata.CloudEvent.Type);
        Assert.Equal("test/subject", metadata.CloudEvent.Subject);
    }

    [Fact]
    public void CommandResponseMetadata_CloudEventProperty_CanBeSetAndRetrieved()
    {
        // Arrange
        var cloudEvent = new CloudEvent(new Uri("aio://test/response"), "ms.aio.rpc.response")
        {
            Id = "test-response-id",
            Subject = "test/response/subject"
        };

        // Act
        var metadata = new CommandResponseMetadata
        {
            CloudEvent = cloudEvent
        };

        // Assert
        Assert.NotNull(metadata.CloudEvent);
        Assert.Equal("test-response-id", metadata.CloudEvent.Id);
        Assert.Equal("ms.aio.rpc.response", metadata.CloudEvent.Type);
        Assert.Equal("test/response/subject", metadata.CloudEvent.Subject);
    }

    [Fact]
    public void CommandRequestMetadata_MarshalWithMinimalCloudEvent()
    {
        // Arrange
        var cloudEvent = new CloudEvent(new Uri("aio://test"), "custom-type");
        var metadata = new CommandRequestMetadata { CloudEvent = cloudEvent };
        var message = new MqttApplicationMessage("test/topic");

        // Act
        metadata.MarshalTo(message);

        // Assert
        Assert.NotNull(message.UserProperties);
        Assert.Contains(message.UserProperties, p => p.Name == "specversion" && p.Value == "1.0");
        Assert.Contains(message.UserProperties, p => p.Name == "type" && p.Value == "custom-type");
        Assert.Contains(message.UserProperties, p => p.Name == "source" && p.Value == "aio://test/");
        // Only required fields should be present
        Assert.Equal(3, message.UserProperties.Count);
    }

    [Fact]
    public void CommandResponseMetadata_MarshalWithMinimalCloudEvent()
    {
        // Arrange
        var cloudEvent = new CloudEvent(new Uri("aio://test"), "custom-response-type");
        var metadata = new CommandResponseMetadata { CloudEvent = cloudEvent };
        var message = new MqttApplicationMessage("test/topic");

        // Act
        metadata.MarshalTo(message);

        // Assert
        Assert.NotNull(message.UserProperties);
        Assert.Contains(message.UserProperties, p => p.Name == "specversion" && p.Value == "1.0");
        Assert.Contains(message.UserProperties, p => p.Name == "type" && p.Value == "custom-response-type");
        Assert.Contains(message.UserProperties, p => p.Name == "source" && p.Value == "aio://test/");
        // Only required fields should be present
        Assert.Equal(3, message.UserProperties.Count);
    }
}
