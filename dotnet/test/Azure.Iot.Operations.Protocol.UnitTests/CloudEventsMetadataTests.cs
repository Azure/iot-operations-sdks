// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Telemetry;

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
}
