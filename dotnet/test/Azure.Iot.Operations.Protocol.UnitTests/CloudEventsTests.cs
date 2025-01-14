// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Telemetry;
using System;
using Xunit;

namespace Azure.Iot.Operations.Protocol.Tests.Telemetry;

public class CloudEventsTests
{
    [Fact]
    public void DefaultValues()
    {
        var metadata = new CloudEvent(new Uri("a", UriKind.RelativeOrAbsolute), "tel-type");
        
        Assert.Equal("1.0", metadata.SpecVersion);
        Assert.Equal("tel-type", metadata.Type);
        Assert.Equal("a", metadata.Source!.ToString());
        Assert.NotNull(metadata.Id);

        Assert.Null(metadata.DataContentType);
        Assert.Null(metadata.DataSchema);
        Assert.Null(metadata.Subject);
        Assert.NotNull(metadata.Time);
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

        var metadata = new CloudEvent(source, type, specVersion);
        metadata.Id = id;
        metadata.DataContentType = dataContentType;
        metadata.DataSchema = dataSchema;
        metadata.Subject = subject;
        metadata.Time = time;

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
    public void FromMqttMessageUserProperties()
    {
        string id = Guid.NewGuid().ToString();
        DateTime time = new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        string contentType = "application/json";
        var userProperties = new Dictionary<string, string>
        {
            { nameof(CloudEvent.SpecVersion).ToLowerInvariant(), "1.0" },
            { nameof(CloudEvent.Type).ToLowerInvariant(), "eventType" },
            { nameof(CloudEvent.Source).ToLowerInvariant(), "my://source" },
            { nameof(CloudEvent.Subject).ToLowerInvariant(), "eventSubject" },
            { nameof(CloudEvent.DataSchema).ToLowerInvariant(), "eventSchema" },
            { nameof(CloudEvent.Id).ToLowerInvariant(), id },
            { nameof(CloudEvent.Time).ToLowerInvariant(), time.ToString("O") },
        };

        CloudEvent? cloudEvent = new CloudEvent(contentType, userProperties);

        Assert.NotNull(cloudEvent);
        Assert.Equal("1.0", cloudEvent.SpecVersion);
        Assert.Equal("eventType", cloudEvent.Type);
        Assert.Equal(new Uri("my://source"), cloudEvent.Source);
        Assert.Equal("eventSubject", cloudEvent.Subject);
        Assert.Equal("eventSchema", cloudEvent.DataSchema);
        Assert.Equal("application/json", cloudEvent.DataContentType);
        Assert.Equal(id, cloudEvent.Id);
        Assert.Equal(time.ToUniversalTime(), cloudEvent.Time!.Value.ToUniversalTime());
    }

    [Fact]
    public void ToUserPropertiesAndContentType()
    {
        var id = "123";
        var source = new Uri("https://example.com");
        var specVersion = "2.0";
        var type = "custom.type";
        var dataContentType = "application/json";
        var dataSchema = "https://schema.example.com";
        var subject = "test";
        var time = DateTime.Now;

        var cloudEvent = new CloudEvent(source, type, specVersion);
        cloudEvent.Id = id;
        cloudEvent.DataContentType = dataContentType;
        cloudEvent.DataSchema = dataSchema;
        cloudEvent.Subject = subject;
        cloudEvent.Time = time;

        Dictionary<string, string> userProperties = cloudEvent.ToUserProperties();
        Assert.True(userProperties.ContainsKey("specversion"));
        Assert.Equal(specVersion, userProperties["specversion"]);
        Assert.True(userProperties.ContainsKey("id"));
        Assert.Equal(id, userProperties["id"]);
        Assert.True(userProperties.ContainsKey("type"));
        Assert.Equal(type, userProperties["type"]);
        Assert.True(userProperties.ContainsKey("source"));
        Assert.Equal(source.ToString(), userProperties["source"]);
        Assert.True(userProperties.ContainsKey("time"));
        Assert.Equal(time.ToString("O"), userProperties["time"]);
        Assert.True(userProperties.ContainsKey("subject"));
        Assert.Equal(subject, userProperties["subject"]);
        Assert.True(userProperties.ContainsKey("dataschema"));
        Assert.Equal(dataSchema, userProperties["dataschema"]);
    }
}
