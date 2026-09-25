// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.EdgeRegistry;
using Azure.Iot.Operations.Services.EdgeRegistry.Models;
using Xunit;

namespace Azure.Iot.Operations.Services.UnitTests.EdgeRegistry;

public class ResourceIdTests
{
    /// <summary>
    /// Pins the hash to the one the Edge Registry service computes, the lowercase hex SHA-256 of
    /// the input. Every SDK must produce this value for identifiers to be reproducible across
    /// implementations.
    /// </summary>
    /// <remarks>
    /// This doubles as a cross-language test: the Rust <c>hashes_to_lowercase_sha256_hex</c> test in
    /// <c>azure_iot_operations_services::edge_registry</c> asserts the same input yields the same
    /// identifier, so a connector written in either language derives one Resource, not two. Changing
    /// this value here without changing it there silently forks the registry.
    /// </remarks>
    [Fact]
    public void HashesToLowercaseSha256Hex()
    {
        (string resourceId, _) = ResourceId.Derive("Test-Document");

        Assert.Equal("1ae8481659aaf8fe08cb58818b1793849756b0f526d835e5106cfb76e558cfdd", resourceId);
    }

    [Theory]
    [InlineData("abc")] // shortest permitted
    [InlineData("123")] // digits only
    [InlineData("a-b-c")] // internal hyphens
    [InlineData("opcua-asset-td")] // typical identifier
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] // longest permitted (64)
    public void UsesAConformingIdentifierAsTheResourceId(string originalId)
    {
        (string resourceId, IReadOnlyList<Label> resourceLabels) = ResourceId.Derive(originalId);

        Assert.Equal(originalId, resourceId);
        Assert.Equal(originalId, Assert.Single(resourceLabels).Value);
    }

    [Theory]
    [InlineData("ab")] // shorter than permitted
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] // longer than permitted (65)
    [InlineData("-abc")] // leading hyphen
    [InlineData("abc-")] // trailing hyphen
    [InlineData("Abc")] // uppercase
    [InlineData("a_c")] // underscore
    [InlineData("a\u00f1b")] // non ascii
    [InlineData("urn:azureiot:aio:dev:ep:opcua:asset:td.g")] // wot identifier
    public void HashesANonConformingIdentifier(string originalId)
    {
        (string resourceId, IReadOnlyList<Label> resourceLabels) = ResourceId.Derive(originalId);

        Assert.NotEqual(originalId, resourceId);
        Assert.Equal(64, resourceId.Length);
        Assert.All(resourceId, c => Assert.True(char.IsAsciiDigit(c) || char.IsAsciiLetterLower(c)));
        Assert.Equal(originalId, Assert.Single(resourceLabels).Value);
    }

    [Fact]
    public void DerivationIsDeterministic()
    {
        Assert.Equal(ResourceId.Derive("Some:Original@Id").ResourceId, ResourceId.Derive("Some:Original@Id").ResourceId);
    }

    [Fact]
    public void DistinctIdentifiersDeriveDistinctResourceIds()
    {
        Assert.NotEqual(ResourceId.Derive("Some:Original@Id").ResourceId, ResourceId.Derive("Some:Other@Id").ResourceId);
    }

    [Fact]
    public void RejectsAnEmptyIdentifier()
    {
        Assert.Throws<ArgumentException>(() => ResourceId.Derive(string.Empty));
    }

    [Theory]
    [InlineData("Some:Original@Id_")] // hashed identifier
    [InlineData("opcua-asset-td")] // conforming identifier
    public void RecordsTheOriginalIdVerbatim(string originalId)
    {
        (_, IReadOnlyList<Label> resourceLabels) = ResourceId.Derive(originalId);

        Label label = Assert.Single(resourceLabels);
        Assert.Equal(ResourceId.OriginalIdLabelKey, label.Key);
        Assert.Equal(originalId, label.Value);
    }

    [Fact]
    public void DoesNotDuplicateAnIdenticalOriginalIdLabel()
    {
        List<Label> existing = new() { new Label { Key = ResourceId.OriginalIdLabelKey, Value = "current" } };

        (_, IReadOnlyList<Label> resourceLabels) = ResourceId.Derive("current", existing);

        Assert.Equal("current", Assert.Single(resourceLabels).Value);
    }

    [Fact]
    public void KeepsAnOriginalIdLabelRecordingADifferentIdentifier()
    {
        List<Label> existing = new() { new Label { Key = ResourceId.OriginalIdLabelKey, Value = "other" } };

        (_, IReadOnlyList<Label> resourceLabels) = ResourceId.Derive("current", existing);

        Assert.Equal(2, resourceLabels.Count);
        Assert.Equal("other", resourceLabels[0].Value);
        Assert.Equal("current", resourceLabels[1].Value);
    }

    [Fact]
    public void PreservesUnrelatedLabels()
    {
        List<Label> existing = new()
        {
            new Label { Key = "first", Value = "1" },
            // Shares the value, but not the key, of the label being recorded.
            new Label { Key = "second", Value = "some-id" },
        };

        (_, IReadOnlyList<Label> resourceLabels) = ResourceId.Derive("some-id", existing);

        Assert.Equal(3, resourceLabels.Count);
        Assert.Equal("first", resourceLabels[0].Key);
        Assert.Equal("second", resourceLabels[1].Key);
        Assert.Equal(ResourceId.OriginalIdLabelKey, resourceLabels[2].Key);
    }

    [Fact]
    public void LeavesTheCallersLabelsUnmodified()
    {
        List<Label> existing = new() { new Label { Key = ResourceId.OriginalIdLabelKey, Value = "current" } };

        ResourceId.Derive("current", existing);

        Assert.Equal("current", Assert.Single(existing).Value);
    }
}
