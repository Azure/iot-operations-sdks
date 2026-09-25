// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.EdgeRegistry.Models;
using Xunit;

namespace Azure.Iot.Operations.Services.UnitTests.EdgeRegistry;

public class VersionXIdTests
{
    [Theory]
    [InlineData("/schemagroups/my-group/schemas/my-schema/versions/1")] // leading slash
    [InlineData("schemagroups/my-group/schemas/my-schema/versions/1")] // no leading slash
    public void ParsesAVersionXId(string xid)
    {
        VersionXId parsed = VersionXId.Parse(xid);

        Assert.Equal("schemagroups", parsed.GroupType);
        Assert.Equal("my-group", parsed.GroupId);
        Assert.Equal("schemas", parsed.ResourceType);
        Assert.Equal("my-schema", parsed.ResourceId);
        Assert.Equal("1", parsed.VersionId);
    }

    [Theory]
    [InlineData("")] // empty
    [InlineData("/")] // root
    [InlineData("/schemagroups/my-group/schemas/my-schema/versions")] // too few segments
    [InlineData("/schemagroups/my-group/schemas/my-schema/versions/1/2")] // too many segments
    [InlineData("/schemagroups/my-group/schemas/my-schema/revisions/1")] // missing the versions segment
    [InlineData("/schemagroups//schemas/my-schema/versions/1")] // empty path segment
    [InlineData("/schemagroups/my-group/schemas/my-schema/versions/")] // trailing empty segment
    public void RejectsANonVersionXId(string xid)
    {
        Assert.Throws<FormatException>(() => VersionXId.Parse(xid));
    }
}
