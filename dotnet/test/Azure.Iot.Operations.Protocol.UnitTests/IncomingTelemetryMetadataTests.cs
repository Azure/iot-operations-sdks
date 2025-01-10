// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.Telemetry;

namespace Azure.Iot.Operations.Protocol.UnitTests;

public class IncomingTelemetryMetadataTests
{
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
        Assert.Equal(packetId, metadata.PacketId);
    }
}
