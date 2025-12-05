// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.Telemetry;
using System;
using System.Globalization;

namespace Azure.Iot.Operations.Protocol.Tests.Models;

public class MqttApplicationMessageCloudEventTests
{
    [Fact]
    public void SetCloudEvent_WithAllProperties_SetsUserPropertiesAndContentType()
    {
        // Arrange
        var message = new MqttApplicationMessage("test/topic");
        var cloudEvent = new CloudEvent(new Uri("test://source"), "custom.type")
        {
            Id = "test-id",
            Time = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            DataContentType = "application/json",
            Subject = "test-subject",
            DataSchema = "https://schema.example.com"
        };

        // Act
        message.SetCloudEvent(cloudEvent);

        // Assert
        Assert.NotNull(message.UserProperties);
        Assert.Contains(message.UserProperties, p => p.Name == "specversion" && p.Value == "1.0");
        Assert.Contains(message.UserProperties, p => p.Name == "id" && p.Value == "test-id");
        Assert.Contains(message.UserProperties, p => p.Name == "source" && p.Value == "test://source/");
        Assert.Contains(message.UserProperties, p => p.Name == "type" && p.Value == "custom.type");
        Assert.Contains(message.UserProperties, p => p.Name == "subject" && p.Value == "test-subject");
        Assert.Contains(message.UserProperties, p => p.Name == "dataschema" && p.Value == "https://schema.example.com");
        Assert.Contains(message.UserProperties, p => p.Name == "time");
        Assert.Equal("application/json", message.ContentType);
    }

    [Fact]
    public void SetCloudEvent_WithoutId_GeneratesGuid()
    {
        // Arrange
        var message = new MqttApplicationMessage("test/topic");
        var cloudEvent = new CloudEvent(new Uri("test://source"), "custom.type");

        // Act
        message.SetCloudEvent(cloudEvent);

        // Assert
        Assert.NotNull(cloudEvent.Id);
        Assert.True(Guid.TryParse(cloudEvent.Id, out _));
        Assert.Contains(message.UserProperties!, p => p.Name == "id" && !string.IsNullOrEmpty(p.Value));
    }

    [Fact]
    public void SetCloudEvent_WithoutTime_SetsCurrentTime()
    {
        // Arrange
        var message = new MqttApplicationMessage("test/topic");
        var cloudEvent = new CloudEvent(new Uri("test://source"), "custom.type");
        var before = DateTime.UtcNow;

        // Act
        message.SetCloudEvent(cloudEvent);
        var after = DateTime.UtcNow;

        // Assert
        Assert.NotNull(cloudEvent.Time);
        Assert.True(cloudEvent.Time >= before && cloudEvent.Time <= after);
        Assert.Contains(message.UserProperties!, p => p.Name == "time");
    }

    [Fact]
    public void SetCloudEvent_WithNullCloudEvent_ThrowsArgumentNullException()
    {
        // Arrange
        var message = new MqttApplicationMessage("test/topic");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => message.SetCloudEvent(null!));
    }

    [Fact]
    public void SetCloudEvent_OverridesContentType()
    {
        // Arrange
        var message = new MqttApplicationMessage("test/topic")
        {
            ContentType = "text/plain"
        };
        var cloudEvent = new CloudEvent(new Uri("test://source"), "custom.type")
        {
            DataContentType = "application/xml"
        };

        // Act
        message.SetCloudEvent(cloudEvent);

        // Assert
        Assert.Equal("application/xml", message.ContentType);
    }

    [Fact]
    public void GetCloudEvent_WithValidCloudEvent_ReturnsCloudEvent()
    {
        // Arrange
        var message = new MqttApplicationMessage("test/topic");
        var originalCloudEvent = new CloudEvent(new Uri("test://source"), "custom.type")
        {
            Id = "test-id",
            Time = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            DataContentType = "application/json",
            Subject = "test-subject",
            DataSchema = "https://schema.example.com"
        };
        message.SetCloudEvent(originalCloudEvent);

        // Act
        var retrievedCloudEvent = message.GetCloudEvent();

        // Assert
        Assert.NotNull(retrievedCloudEvent);
        Assert.Equal("1.0", retrievedCloudEvent.SpecVersion);
        Assert.Equal("test-id", retrievedCloudEvent.Id);
        Assert.Equal("test://source/", retrievedCloudEvent.Source.ToString());
        Assert.Equal("custom.type", retrievedCloudEvent.Type);
        Assert.Equal("test-subject", retrievedCloudEvent.Subject);
        Assert.Equal("https://schema.example.com", retrievedCloudEvent.DataSchema);
        Assert.Equal("application/json", retrievedCloudEvent.DataContentType);
        Assert.NotNull(retrievedCloudEvent.Time);
    }

    [Fact]
    public void GetCloudEvent_WithoutUserProperties_ReturnsNull()
    {
        // Arrange
        var message = new MqttApplicationMessage("test/topic");

        // Act
        var cloudEvent = message.GetCloudEvent();

        // Assert
        Assert.Null(cloudEvent);
    }

    [Fact]
    public void GetCloudEvent_WithoutRequiredProperties_ReturnsNull()
    {
        // Arrange
        var message = new MqttApplicationMessage("test/topic");
        message.AddUserProperty("specversion", "1.0");
        // Missing source and type

        // Act
        var cloudEvent = message.GetCloudEvent();

        // Assert
        Assert.Null(cloudEvent);
    }

    [Fact]
    public void GetCloudEvent_WithUnsupportedVersion_ReturnsNull()
    {
        // Arrange
        var message = new MqttApplicationMessage("test/topic");
        message.AddUserProperty("specversion", "2.0");
        message.AddUserProperty("source", "test://source");
        message.AddUserProperty("type", "custom.type");

        // Act
        var cloudEvent = message.GetCloudEvent();

        // Assert
        Assert.Null(cloudEvent);
    }

    [Fact]
    public void GetCloudEvent_WithInvalidSource_ReturnsNull()
    {
        // Arrange
        var message = new MqttApplicationMessage("test/topic");
        message.AddUserProperty("specversion", "1.0");
        message.AddUserProperty("source", ""); // Invalid empty source
        message.AddUserProperty("type", "custom.type");

        // Act
        var cloudEvent = message.GetCloudEvent();

        // Assert
        Assert.Null(cloudEvent);
    }

    [Fact]
    public void GetCloudEvent_CaseInsensitivePropertyNames_ReturnsCloudEvent()
    {
        // Arrange
        var message = new MqttApplicationMessage("test/topic");
        message.AddUserProperty("SpecVersion", "1.0");
        message.AddUserProperty("SOURCE", "test://source");
        message.AddUserProperty("Type", "custom.type");
        message.AddUserProperty("ID", "test-id");

        // Act
        var cloudEvent = message.GetCloudEvent();

        // Assert
        Assert.NotNull(cloudEvent);
        Assert.Equal("1.0", cloudEvent.SpecVersion);
        Assert.Equal("test-id", cloudEvent.Id);
        Assert.Equal("test://source/", cloudEvent.Source.ToString());
        Assert.Equal("custom.type", cloudEvent.Type);
    }

    [Fact]
    public void SetAndGetCloudEvent_RoundTrip_PreservesAllProperties()
    {
        // Arrange
        var message = new MqttApplicationMessage("test/topic");
        var originalCloudEvent = new CloudEvent(new Uri("test://source"), "custom.type")
        {
            Id = "round-trip-id",
            Time = new DateTime(2024, 6, 15, 10, 30, 45, DateTimeKind.Utc),
            DataContentType = "application/avro",
            Subject = "round-trip-subject",
            DataSchema = "https://schema.example.com/v2"
        };

        // Act
        message.SetCloudEvent(originalCloudEvent);
        var retrievedCloudEvent = message.GetCloudEvent();

        // Assert
        Assert.NotNull(retrievedCloudEvent);
        Assert.Equal(originalCloudEvent.SpecVersion, retrievedCloudEvent.SpecVersion);
        Assert.Equal(originalCloudEvent.Id, retrievedCloudEvent.Id);
        Assert.Equal(originalCloudEvent.Source.ToString(), retrievedCloudEvent.Source.ToString());
        Assert.Equal(originalCloudEvent.Type, retrievedCloudEvent.Type);
        Assert.Equal(originalCloudEvent.Subject, retrievedCloudEvent.Subject);
        Assert.Equal(originalCloudEvent.DataSchema, retrievedCloudEvent.DataSchema);
        Assert.Equal(originalCloudEvent.DataContentType, retrievedCloudEvent.DataContentType);
        Assert.Equal(originalCloudEvent.Time, retrievedCloudEvent.Time);
    }
}
