// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
        Assert.NotNull(metadata.Id);
        Assert.True(Guid.TryParse(metadata.Id, out _));

        Assert.Null(metadata.DataSchema);
        Assert.NotNull(metadata.Subject);
        Assert.Equal("", metadata.Subject);
        Assert.NotNull(metadata.Time);
    }

    [Fact]
    public void SetValues()
    {
        var id = "123";
        var source = new Uri("https://example.com");
        var specVersion = "2.0";
        var type = "custom.type";
        var dataSchema = "https://schema.example.com";
        var subject = "test";
        var time = DateTime.Now;

        var metadata = new CloudEvent(source, type, specVersion)
        {
            Id = id,
            DataSchema = dataSchema,
            Subject = subject,
            Time = time
        };

        Assert.Equal(id, metadata.Id);
        Assert.Equal(source, metadata.Source);
        Assert.Equal(specVersion, metadata.SpecVersion);
        Assert.Equal(type, metadata.Type);
        Assert.Equal(dataSchema, metadata.DataSchema);
        Assert.Equal(subject, metadata.Subject);
        Assert.Equal(time, metadata.Time);
    }
}
