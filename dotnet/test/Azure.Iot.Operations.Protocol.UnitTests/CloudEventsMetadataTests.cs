// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Telemetry;
using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Protocol.Tests.Telemetry;

public class CloudEventsMetadataTests
{
    [Fact]
    public void DefaultValues()
    {
        var metadata = new CloudEvent(new Uri("a", UriKind.RelativeOrAbsolute), "tel-type");
        
        Assert.Equal("1.0", metadata.SpecVersion);
        Assert.Equal("tel-type", metadata.Type);
        Assert.Equal("a", metadata.Source!.ToString());
        Assert.Null(metadata.Id);

        Assert.Null(metadata.DataContentType);
        Assert.Null(metadata.DataSchema);
        Assert.Null(metadata.Subject);
        Assert.Null(metadata.Time);
    }

    [Fact]
    public void SetValues()
    {
        var id = "123";
        var source = new Uri("https://example.com");
        var specVersion = "2.0";
        var type = "custom.type";
        var dataContentType = "application/json";
        var dataSchema = "https://schema.example.com";
        var subject = "test";
        var time = DateTime.Now;

        var metadata = new CloudEvent(source, type, specVersion)
        {
            Id = id,
            DataContentType = dataContentType,
            DataSchema = dataSchema,
            Subject = subject,
            Time = time
        };

        Assert.Equal(id, metadata.Id);
        Assert.Equal(source, metadata.Source);
        Assert.Equal(specVersion, metadata.SpecVersion);
        Assert.Equal(type, metadata.Type);
        Assert.Equal(dataContentType, metadata.DataContentType);
        Assert.Equal(dataSchema, metadata.DataSchema);
        Assert.Equal(subject, metadata.Subject);
        Assert.Equal(time, metadata.Time);
    }

    [Fact]
    public void CreateMqttMessageContext_WithAllFieldsSet_ReturnsCorrectContext()
    {
        // Arrange
        var id = "test-id-123";
        var source = new Uri("aio://test/source");
        var type = "test.type";
        var specVersion = "1.0";
        var subject = "test/subject";
        var dataSchema = "https://schema.example.com";
        var dataContentType = "application/json";
        var time = new DateTime(2025, 12, 2, 10, 30, 0, DateTimeKind.Utc);

        var cloudEvent = new CloudEvent(source, type, specVersion)
        {
            Id = id,
            Subject = subject,
            DataSchema = dataSchema,
            DataContentType = dataContentType,
            Time = time
        };

        // Act
        var context = cloudEvent.CreateMqttMessageContext();

        // Assert
        Assert.NotNull(context);
        Assert.NotNull(context.MqttUserProperties);
        Assert.Equal(dataContentType, context.MqttMessageContentType);

        // Verify all required properties are present
        Assert.Contains(context.MqttUserProperties, p => p.Name == "id" && p.Value == id);
        Assert.Contains(context.MqttUserProperties, p => p.Name == "source" && p.Value == source.ToString());
        Assert.Contains(context.MqttUserProperties, p => p.Name == "type" && p.Value == type);
        Assert.Contains(context.MqttUserProperties, p => p.Name == "specversion" && p.Value == specVersion);
        Assert.Contains(context.MqttUserProperties, p => p.Name == "subject" && p.Value == subject);
        Assert.Contains(context.MqttUserProperties, p => p.Name == "dataschema" && p.Value == dataSchema);
        Assert.Contains(context.MqttUserProperties, p => p.Name == "time" && p.Value == "2025-12-02T10:30:00Z");
    }

    [Fact]
    public void CreateMqttMessageContext_WithMinimalFields_GeneratesDefaults()
    {
        // Arrange
        var source = new Uri("aio://test/source");
        var type = "test.type";
        var cloudEvent = new CloudEvent(source, type);

        // Act
        var context = cloudEvent.CreateMqttMessageContext();

        // Assert
        Assert.NotNull(context);
        Assert.NotNull(context.MqttUserProperties);

        // Verify required fields are present
        Assert.Contains(context.MqttUserProperties, p => p.Name == "source" && p.Value == source.ToString());
        Assert.Contains(context.MqttUserProperties, p => p.Name == "type" && p.Value == type);
        Assert.Contains(context.MqttUserProperties, p => p.Name == "specversion" && p.Value == "1.0");

        // Verify id was generated (should be a valid GUID)
        var idProperty = context.MqttUserProperties.FirstOrDefault(p => p.Name == "id");
        Assert.NotNull(idProperty);
        Assert.True(Guid.TryParse(idProperty.Value, out _), "Generated id should be a valid GUID");

        // Verify time was generated (should be parseable)
        var timeProperty = context.MqttUserProperties.FirstOrDefault(p => p.Name == "time");
        Assert.NotNull(timeProperty);
        Assert.True(DateTime.TryParse(timeProperty.Value, out _), "Generated time should be a valid date-time");
    }

    [Fact]
    public void CreateMqttMessageContext_WithoutSubject_DoesNotIncludeSubjectProperty()
    {
        // Arrange
        var source = new Uri("aio://test/source");
        var cloudEvent = new CloudEvent(source);

        // Act
        var context = cloudEvent.CreateMqttMessageContext();

        // Assert
        Assert.DoesNotContain(context.MqttUserProperties, p => p.Name == "subject");
    }

    [Fact]
    public void CreateMqttMessageContext_WithoutDataSchema_DoesNotIncludeDataSchemaProperty()
    {
        // Arrange
        var source = new Uri("aio://test/source");
        var cloudEvent = new CloudEvent(source);

        // Act
        var context = cloudEvent.CreateMqttMessageContext();

        // Assert
        Assert.DoesNotContain(context.MqttUserProperties, p => p.Name == "dataschema");
    }

    [Fact]
    public void CreateFromMqttUserProperties_WithValidProperties_ReturnsCloudEvent()
    {
        // Arrange
        var mqttContext = new CloudEventMqttContext
        {
            MqttUserProperties = new List<MqttUserProperty>
            {
                new MqttUserProperty("id", "test-id-456"),
                new MqttUserProperty("source", "aio://test/source"),
                new MqttUserProperty("type", "test.type"),
                new MqttUserProperty("specversion", "1.0"),
                new MqttUserProperty("time", "2025-12-02T15:45:30+00:00"),
                new MqttUserProperty("subject", "test/subject"),
                new MqttUserProperty("dataschema", "https://schema.example.com")
            },
            MqttMessageContentType = "application/json"
        };

        // Act
        var cloudEvent = CloudEvent.CreateFromMqttUserProperties(mqttContext);

        // Assert
        Assert.NotNull(cloudEvent);
        Assert.Equal("test-id-456", cloudEvent.Id);
        Assert.Equal("aio://test/source", cloudEvent.Source.ToString());
        Assert.Equal("test.type", cloudEvent.Type);
        Assert.Equal("1.0", cloudEvent.SpecVersion);
        Assert.Equal("test/subject", cloudEvent.Subject);
        Assert.Equal("https://schema.example.com", cloudEvent.DataSchema);
        Assert.Equal("application/json", cloudEvent.DataContentType);
        Assert.NotNull(cloudEvent.Time);
        Assert.Equal(new DateTime(2025, 12, 2, 15, 45, 30, DateTimeKind.Utc), cloudEvent.Time!.Value);
    }

    [Fact]
    public void CreateFromMqttUserProperties_WithMinimalFields_ReturnsCloudEvent()
    {
        // Arrange
        var mqttContext = new CloudEventMqttContext
        {
            MqttUserProperties = new List<MqttUserProperty>
            {
                new MqttUserProperty("id", "test-id-789"),
                new MqttUserProperty("source", "aio://test/source"),
                new MqttUserProperty("type", "test.type"),
                new MqttUserProperty("specversion", "1.0")
            }
        };

        // Act
        var cloudEvent = CloudEvent.CreateFromMqttUserProperties(mqttContext);

        // Assert
        Assert.NotNull(cloudEvent);
        Assert.Equal("test-id-789", cloudEvent.Id);
        Assert.Equal("aio://test/source", cloudEvent.Source.ToString());
        Assert.Equal("test.type", cloudEvent.Type);
        Assert.Equal("1.0", cloudEvent.SpecVersion);
        Assert.Null(cloudEvent.Subject);
        Assert.Null(cloudEvent.DataSchema);
        Assert.Null(cloudEvent.DataContentType);
        Assert.Null(cloudEvent.Time);
    }

    [Fact]
    public void CreateFromMqttUserProperties_MissingId_ThrowsArgumentException()
    {
        // Arrange
        var mqttContext = new CloudEventMqttContext
        {
            MqttUserProperties = new List<MqttUserProperty>
            {
                new MqttUserProperty("source", "aio://test/source"),
                new MqttUserProperty("type", "test.type"),
                new MqttUserProperty("specversion", "1.0")
            }
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => CloudEvent.CreateFromMqttUserProperties(mqttContext));
        Assert.Contains("'id' is required", exception.Message);
    }

    [Fact]
    public void CreateFromMqttUserProperties_MissingSource_ThrowsArgumentException()
    {
        // Arrange
        var mqttContext = new CloudEventMqttContext
        {
            MqttUserProperties = new List<MqttUserProperty>
            {
                new MqttUserProperty("id", "test-id"),
                new MqttUserProperty("type", "test.type"),
                new MqttUserProperty("specversion", "1.0")
            }
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => CloudEvent.CreateFromMqttUserProperties(mqttContext));
        Assert.Contains("'source' is required", exception.Message);
    }

    [Fact]
    public void CreateFromMqttUserProperties_MissingType_ThrowsArgumentException()
    {
        // Arrange
        var mqttContext = new CloudEventMqttContext
        {
            MqttUserProperties = new List<MqttUserProperty>
            {
                new MqttUserProperty("id", "test-id"),
                new MqttUserProperty("source", "aio://test/source"),
                new MqttUserProperty("specversion", "1.0")
            }
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => CloudEvent.CreateFromMqttUserProperties(mqttContext));
        Assert.Contains("'type' is required", exception.Message);
    }

    [Fact]
    public void CreateFromMqttUserProperties_MissingSpecVersion_ThrowsArgumentException()
    {
        // Arrange
        var mqttContext = new CloudEventMqttContext
        {
            MqttUserProperties = new List<MqttUserProperty>
            {
                new MqttUserProperty("id", "test-id"),
                new MqttUserProperty("source", "aio://test/source"),
                new MqttUserProperty("type", "test.type")
            }
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => CloudEvent.CreateFromMqttUserProperties(mqttContext));
        Assert.Contains("'specversion' is required", exception.Message);
    }

    [Fact]
    public void CreateFromMqttUserProperties_InvalidSpecVersion_ThrowsArgumentException()
    {
        // Arrange
        var mqttContext = new CloudEventMqttContext
        {
            MqttUserProperties = new List<MqttUserProperty>
            {
                new MqttUserProperty("id", "test-id"),
                new MqttUserProperty("source", "aio://test/source"),
                new MqttUserProperty("type", "test.type"),
                new MqttUserProperty("specversion", "2.0")
            }
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => CloudEvent.CreateFromMqttUserProperties(mqttContext));
        Assert.Contains("Only CloudEvent spec version 1.0 is supported", exception.Message);
    }

    [Fact]
    public void CreateFromMqttUserProperties_WithRelativeSource_Succeeds()
    {
        // Arrange - relative URIs are valid per CloudEvents spec
        var mqttContext = new CloudEventMqttContext
        {
            MqttUserProperties = new List<MqttUserProperty>
            {
                new MqttUserProperty("id", "test-id"),
                new MqttUserProperty("source", "relative/path"),
                new MqttUserProperty("type", "test.type"),
                new MqttUserProperty("specversion", "1.0")
            }
        };

        // Act
        var cloudEvent = CloudEvent.CreateFromMqttUserProperties(mqttContext);

        // Assert
        Assert.NotNull(cloudEvent);
        Assert.Equal("relative/path", cloudEvent.Source.ToString());
    }

    [Fact]
    public void CreateFromMqttUserProperties_InvalidTime_ThrowsArgumentException()
    {
        // Arrange
        var mqttContext = new CloudEventMqttContext
        {
            MqttUserProperties = new List<MqttUserProperty>
            {
                new MqttUserProperty("id", "test-id"),
                new MqttUserProperty("source", "aio://test/source"),
                new MqttUserProperty("type", "test.type"),
                new MqttUserProperty("specversion", "1.0"),
                new MqttUserProperty("time", "not-a-valid-date")
            }
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => CloudEvent.CreateFromMqttUserProperties(mqttContext));
        Assert.Contains("'time' must be a valid RFC3339 date-time", exception.Message);
    }

    [Fact]
    public void CreateFromMqttUserProperties_NullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => CloudEvent.CreateFromMqttUserProperties(null!));
    }

    [Fact]
    public void CreateFromMqttUserProperties_NullUserProperties_ThrowsArgumentException()
    {
        // Arrange
        var mqttContext = new CloudEventMqttContext
        {
            MqttUserProperties = null!
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => CloudEvent.CreateFromMqttUserProperties(mqttContext));
        Assert.Contains("MqttUserProperties cannot be null", exception.Message);
    }

    [Fact]
    public void CreateMqttMessageContext_AndCreateFromMqttUserProperties_RoundTrip()
    {
        // Arrange
        var originalCloudEvent = new CloudEvent(new Uri("aio://test/source"), "test.type")
        {
            Id = "test-round-trip-id",
            Subject = "test/subject",
            DataSchema = "https://schema.example.com",
            DataContentType = "application/json",
            Time = new DateTime(2025, 12, 2, 12, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var context = originalCloudEvent.CreateMqttMessageContext();
        var parsedCloudEvent = CloudEvent.CreateFromMqttUserProperties(context);

        // Assert
        Assert.Equal(originalCloudEvent.Id, parsedCloudEvent.Id);
        Assert.Equal(originalCloudEvent.Source.ToString(), parsedCloudEvent.Source.ToString());
        Assert.Equal(originalCloudEvent.Type, parsedCloudEvent.Type);
        Assert.Equal(originalCloudEvent.SpecVersion, parsedCloudEvent.SpecVersion);
        Assert.Equal(originalCloudEvent.Subject, parsedCloudEvent.Subject);
        Assert.Equal(originalCloudEvent.DataSchema, parsedCloudEvent.DataSchema);
        Assert.Equal(originalCloudEvent.DataContentType, parsedCloudEvent.DataContentType);
        Assert.Equal(originalCloudEvent.Time, parsedCloudEvent.Time);
    }
}
