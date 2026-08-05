// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Chunking;

namespace Azure.Iot.Operations.Protocol.UnitTests.Chunking;

public class ChunkMetadataTests
{
    private const string MessageId = "8ac7a0e4-1b3d-4f9a-9a3f-0d2f6c5b7e11";
    private const string Checksum = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    [Fact]
    public void Format_FirstChunk_EmitsAllFields()
    {
        var metadata = ChunkMetadata.CreateFirstChunk(MessageId, 4, Checksum);

        Assert.Equal($"{MessageId}:0:4:{Checksum}", metadata.Format());
    }

    [Fact]
    public void Format_SubsequentChunk_EmitsMessageIdAndIndexOnly()
    {
        var metadata = ChunkMetadata.CreateSubsequentChunk(MessageId, 3);

        Assert.Equal($"{MessageId}:3", metadata.Format());
    }

    [Fact]
    public void TryParse_FirstChunk_RoundTrips()
    {
        var original = ChunkMetadata.CreateFirstChunk(MessageId, 4, Checksum);

        Assert.True(ChunkMetadata.TryParse(original.Format(), out var parsed));
        Assert.Equal(MessageId, parsed!.MessageId);
        Assert.Equal(0, parsed.ChunkIndex);
        Assert.Equal(4, parsed.TotalChunks);
        Assert.Equal(Checksum, parsed.Checksum);
    }

    [Fact]
    public void TryParse_SubsequentChunk_RoundTrips()
    {
        var original = ChunkMetadata.CreateSubsequentChunk(MessageId, 3);

        Assert.True(ChunkMetadata.TryParse(original.Format(), out var parsed));
        Assert.Equal(MessageId, parsed!.MessageId);
        Assert.Equal(3, parsed.ChunkIndex);
        Assert.Null(parsed.TotalChunks);
        Assert.Null(parsed.Checksum);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("only-one-field")]
    [InlineData("id:0:4")]
    [InlineData("id:0:4:checksum:extra")]
    [InlineData(":1")]
    [InlineData("id:notanumber")]
    [InlineData("id:-1")]
    [InlineData("id: 1")]
    public void TryParse_MalformedValue_Fails(string? value)
    {
        Assert.False(ChunkMetadata.TryParse(value, out var parsed));
        Assert.Null(parsed);
    }

    [Fact]
    public void TryParse_SubsequentChunkFormAtIndexZero_Fails()
    {
        // Index 0 must use the first-chunk form, which carries totalChunks and checksum.
        Assert.False(ChunkMetadata.TryParse($"{MessageId}:0", out var parsed));
        Assert.Null(parsed);
    }

    [Fact]
    public void TryParse_FirstChunkFormAtNonZeroIndex_Fails()
    {
        Assert.False(ChunkMetadata.TryParse($"{MessageId}:1:4:{Checksum}", out var parsed));
        Assert.Null(parsed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void TryParse_NonPositiveTotalChunks_Fails(int totalChunks)
    {
        Assert.False(ChunkMetadata.TryParse($"{MessageId}:0:{totalChunks}:{Checksum}", out var parsed));
        Assert.Null(parsed);
    }

    [Fact]
    public void TryParse_EmptyChecksum_Fails()
    {
        Assert.False(ChunkMetadata.TryParse($"{MessageId}:0:4:", out var parsed));
        Assert.Null(parsed);
    }
}
