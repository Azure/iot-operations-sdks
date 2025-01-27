// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.Telemetry;

namespace Azure.Iot.Operations.Protocol.UnitTests;

public class IncomingTelemetryMetadataTests
{
    [Fact]
    public void WithValidCloudEvent()
    {
        string id = Guid.NewGuid().ToString();
        DateTime time = new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var message = new MqttApplicationMessage("someTopic")
        {
            CorrelationData = Guid.NewGuid().ToByteArray(),
            UserProperties = new List<MqttUserProperty>
            {
                new MqttUserProperty(nameof(CloudEvent.SpecVersion).ToLowerInvariant(), "1.0"),
                new MqttUserProperty(nameof(CloudEvent.Type).ToLowerInvariant(), "eventType"),
                new MqttUserProperty(nameof(CloudEvent.Source).ToLowerInvariant(), "my://source"),
                new MqttUserProperty(nameof(CloudEvent.Subject).ToLowerInvariant(), "eventSubject"),
                new MqttUserProperty(nameof(CloudEvent.DataSchema).ToLowerInvariant(), "eventSchema"),
                new MqttUserProperty(nameof(CloudEvent.DataContentType).ToLowerInvariant(), "application/json"),
                new MqttUserProperty(nameof(CloudEvent.Id).ToLowerInvariant(), id),
                new MqttUserProperty(nameof(CloudEvent.Time).ToLowerInvariant(), time.ToString("O")),
                new MqttUserProperty("customProperty", "customValue")
            }
        };
        uint packetId = 123;

        
        var metadata = new IncomingTelemetryMetadata(message, packetId);

        
        Assert.Null(metadata.Timestamp);
        Assert.NotNull(metadata.UserData);
        Assert.NotNull(metadata.GetCloudEvent());
        Assert.Equal(packetId, metadata.PacketId);

        Assert.Equal("1.0", metadata.GetCloudEvent().SpecVersion);
        Assert.Equal("eventType", metadata.GetCloudEvent().Type);
        Assert.Equal(new Uri("my://source"), metadata.GetCloudEvent().Source);
        Assert.Equal("eventSubject", metadata.GetCloudEvent().Subject);
        Assert.Equal("eventSchema", metadata.GetCloudEvent().DataSchema);
        Assert.Equal("application/json", metadata.GetCloudEvent().DataContentType);
        Assert.Equal(id, metadata.GetCloudEvent().Id);
        Assert.Equal(time.ToUniversalTime(), metadata.GetCloudEvent().Time!.Value.ToUniversalTime());
    }

    [Fact]
    public void WithInvalidCorrelationData_SetsCorrelationIdToNull()
    {
        
        var message = new MqttApplicationMessage("someTopic")
        {
            CorrelationData = [1, 2, 3, 4]
        };
        uint packetId = 123;

        var metadata = new IncomingTelemetryMetadata(message, packetId);

        Assert.Throws<ArgumentException>(() => metadata.GetCloudEvent());
    }

    [Fact]
    public void WithNullUserProperties_SetsUserDataToEmptyDictionary()
    {
        
        var message = new MqttApplicationMessage("someTopic")
        {
            CorrelationData = Guid.NewGuid().ToByteArray(),
            UserProperties = null
        };
        uint packetId = 123;

        
        var metadata = new IncomingTelemetryMetadata(message, packetId);

        
        Assert.Null(metadata.Timestamp);
        Assert.Empty(metadata.UserData);
        Assert.Equal(packetId, metadata.PacketId);
    }

    [Fact]
    public void WithInvalidCloudEventsMetadata_SetsCloudEventsMetadataToNull()
    {
        
        var message = new MqttApplicationMessage("someTopic")
        {
            CorrelationData = Guid.NewGuid().ToByteArray(),
            UserProperties = new List<MqttUserProperty>
            {
                new MqttUserProperty(nameof(CloudEvent.SpecVersion).ToLowerInvariant(), "2")
            }
        };
        uint packetId = 123;

        
        var metadata = new IncomingTelemetryMetadata(message, packetId);
        
        Assert.Null(metadata.Timestamp);
        Assert.NotNull(metadata.UserData);
        Assert.Throws<ArgumentException>(() => metadata.GetCloudEvent());
        Assert.Equal(packetId, metadata.PacketId);
    }

    [Fact]
    public void WithInvalidCloudEventsType_time_ReturnsNull()
    {

        var message = new MqttApplicationMessage("someTopic")
        {
            UserProperties = new List<MqttUserProperty>
            {
                new MqttUserProperty(nameof(CloudEvent.SpecVersion).ToLowerInvariant(), "1.0"),
                new MqttUserProperty(nameof(CloudEvent.Time).ToLowerInvariant(), "not-a-date")
            }
        };
        uint packetId = 123;


        var metadata = new IncomingTelemetryMetadata(message, packetId);

        Assert.Null(metadata.Timestamp);
        Assert.Throws<ArgumentException>(() => metadata.GetCloudEvent());
        Assert.Equal(packetId, metadata.PacketId);
    }

    [Fact]
    public void WithInvalidCloudEventsType_source_ReturnsNull()
    {

        var message = new MqttApplicationMessage("someTopic")
        {
            UserProperties = new List<MqttUserProperty>
            {
                new MqttUserProperty(nameof(CloudEvent.SpecVersion).ToLowerInvariant(), "test"),
                new MqttUserProperty(nameof(CloudEvent.Source).ToLowerInvariant(), "not-a-uri:??sss")
            }
        };
        uint packetId = 123;


        var metadata = new IncomingTelemetryMetadata(message, packetId);

        Assert.Null(metadata.Timestamp);
        Assert.Throws<ArgumentException>(() => metadata.GetCloudEvent());
        Assert.Equal(packetId, metadata.PacketId);
    }

    [Fact]
    public void WithRequiredCloudEvents_Returns()
    {

        var message = new MqttApplicationMessage("someTopic")
        {
            UserProperties = new List<MqttUserProperty>
            {
                new MqttUserProperty(nameof(CloudEvent.SpecVersion).ToLowerInvariant(), "1.0"),
                new MqttUserProperty(nameof(CloudEvent.Id).ToLowerInvariant(), "123"),
                new MqttUserProperty(nameof(CloudEvent.Source).ToLowerInvariant(), "a/b/c"),
                new MqttUserProperty(nameof(CloudEvent.Type).ToLowerInvariant(), "test"),
            }
        };
        uint packetId = 123;


        var metadata = new IncomingTelemetryMetadata(message, packetId);

        Assert.Equal(packetId, metadata.PacketId);
        Assert.NotNull(metadata.GetCloudEvent());
        Assert.Equal("1.0", metadata.GetCloudEvent().SpecVersion);
        Assert.Equal("123", metadata.GetCloudEvent().Id!.ToString());
        Assert.Equal(new Uri("a/b/c", UriKind.RelativeOrAbsolute), metadata.GetCloudEvent().Source);
        Assert.Equal("test", metadata.GetCloudEvent().Type);
    }

}
