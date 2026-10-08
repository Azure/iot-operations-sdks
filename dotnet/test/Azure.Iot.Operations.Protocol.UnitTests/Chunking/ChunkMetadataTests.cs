// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Chunking;

namespace Azure.Iot.Operations.Protocol.UnitTests.Chunking;

public class ChunkMetadataTests
{
    private const string MessageId = "8ac7a0e4-1b3d-4f9a-9a3f-0d2f6c5b7e11";
    private const string ChecksumId = "sha256";
    private const string Checksum = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    [Fact]
    public void Format_HeadChunk_IsTaggedAndCarriesTheHeader()
    {
        var metadata = ChunkMetadata.CreateFirstChunk(MessageId, 4, ChecksumId, Checksum);

        Assert.Equal($"h:{MessageId}:0:4:{ChecksumId}:{Checksum}", metadata.Format());
    }

    [Fact]
    public void Format_PropertyChunk_IsTaggedAndCarriesTotal()
    {
        var metadata = ChunkMetadata.CreatePropertyChunk(MessageId, 2, 4);

        Assert.Equal($"p:{MessageId}:2:4", metadata.Format());
    }

    [Fact]
    public void Format_DataChunk_IsTaggedAndCarriesTotal()
    {
        var metadata = ChunkMetadata.CreateDataChunk(MessageId, 3, 4);

        Assert.Equal($"d:{MessageId}:3:4", metadata.Format());
    }

    [Theory]
    [InlineData("sha:256", Checksum)]
    [InlineData(ChecksumId, "DEADBEEF")]
    [InlineData(ChecksumId, "")]
    public void Format_HeadChunkWithInvalidChecksumMetadata_Throws(string checksumId, string checksum)
    {
        var metadata = ChunkMetadata.CreateFirstChunk(MessageId, 4, checksumId, checksum);

        Assert.Throws<ArgumentException>(() => metadata.Format());
    }

    [Fact]
    public void TryParse_HeadChunk_RoundTrips()
    {
        var original = ChunkMetadata.CreateFirstChunk(MessageId, 4, ChecksumId, Checksum);

        Assert.True(ChunkMetadata.TryParse(original.Format(), out var parsed));
        Assert.Equal(MessageId, parsed!.MessageId);
        Assert.Equal(0, parsed.ChunkIndex);
        Assert.Equal(4, parsed.TotalChunks);
        Assert.Equal(ChecksumId, parsed.ChecksumId);
        Assert.Equal(Checksum, parsed.Checksum);
    }

    [Fact]
    public void TryParse_DataChunk_RoundTrips()
    {
        var original = ChunkMetadata.CreateDataChunk(MessageId, 3, 4);

        Assert.True(ChunkMetadata.TryParse(original.Format(), out var parsed));
        Assert.Equal(MessageId, parsed!.MessageId);
        Assert.Equal(3, parsed.ChunkIndex);
        Assert.Equal(4, parsed.TotalChunks);
        Assert.Equal(ChunkKind.Data, parsed.Kind);
        Assert.Null(parsed.Checksum);
    }

    [Fact]
    public void TryParse_PropertyChunk_RoundTrips()
    {
        var original = ChunkMetadata.CreatePropertyChunk(MessageId, 2, 4);

        Assert.True(ChunkMetadata.TryParse(original.Format(), out var parsed));
        Assert.Equal(MessageId, parsed!.MessageId);
        Assert.Equal(2, parsed.ChunkIndex);
        Assert.Equal(4, parsed.TotalChunks);
        Assert.Equal(ChunkKind.Property, parsed.Kind);
    }

    [Theory]
    [InlineData("h")]
    [InlineData("p")]
    [InlineData("d")]
    public void TryParse_Countdown_RoundTrips(string kind)
    {
        ChunkMetadata original = kind switch
        {
            "h" => ChunkMetadata.CreateFirstChunk(MessageId, 4, ChecksumId, Checksum),
            "p" => ChunkMetadata.CreatePropertyChunk(MessageId, 2, 4),
            _ => ChunkMetadata.CreateDataChunk(MessageId, 3, 4),
        };

        Assert.True(ChunkMetadata.TryParse(original.Format(27), out ChunkMetadata? parsed));
        Assert.Equal(27u, parsed!.RemainingSeconds);
        Assert.Equal(original.Format(27), parsed.Format());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("only-one-field")]
    // Unknown, missing, or wrong-case tag.
    [InlineData("x:id:1:4")]
    [InlineData("id:1")]
    [InlineData("D:id:1:4")]
    [InlineData("H:id:0:4:sha256:checksum")]
    // Right tag, wrong field count for that form.
    [InlineData("d:id")]
    [InlineData("d:id:1")]
    [InlineData("d:8ac7a0e4-1b3d-4f9a-9a3f-0d2f6c5b7e11:1:4:extra")]
    [InlineData("p:id:1")]
    [InlineData("p:id:1:4:extra")]
    [InlineData("h:id:0:4:sha256")]
    [InlineData("h:id:0:4:sha256:checksum:extra")]
    // Malformed fields.
    [InlineData("d::1:4")]
    [InlineData("d:id:notanumber:4")]
    [InlineData("d:id:-1:4")]
    [InlineData("d:id: 1:4")]
    [InlineData("d:id:1:notanumber")]
    [InlineData("d:id:1:0")]
    [InlineData("d:id:4:4")]
    [InlineData("d:8ac7a0e4-1b3d-4f9a-9a3f-0d2f6c5b7e11:1:4:0")]
    [InlineData("d:8ac7a0e4-1b3d-4f9a-9a3f-0d2f6c5b7e11:1:4:01")]
    [InlineData("d:8AC7A0E4-1B3D-4F9A-9A3F-0D2F6C5B7E11:1:4")]
    [InlineData("h:8ac7a0e4-1b3d-4f9a-9a3f-0d2f6c5b7e11:0:4:sha256:E3B0")]
    public void TryParse_MalformedValue_Fails(string? value)
    {
        Assert.False(ChunkMetadata.TryParse(value, out var parsed));
        Assert.Null(parsed);
    }

    [Fact]
    public void TryParse_DataChunkAtIndexZero_Fails()
    {
        // Index 0 is the head chunk and must use the head form.
        Assert.False(ChunkMetadata.TryParse($"d:{MessageId}:0:4", out var parsed));
        Assert.Null(parsed);
    }

    [Fact]
    public void TryParse_HeadChunkAtNonZeroIndex_Fails()
    {
        Assert.False(ChunkMetadata.TryParse($"h:{MessageId}:1:4:{ChecksumId}:{Checksum}", out var parsed));
        Assert.Null(parsed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void TryParse_NonPositiveTotalChunks_Fails(int totalChunks)
    {
        Assert.False(ChunkMetadata.TryParse($"h:{MessageId}:0:{totalChunks}:{ChecksumId}:{Checksum}", out var parsed));
        Assert.Null(parsed);
    }

    [Fact]
    public void TryParse_EmptyChecksum_Fails()
    {
        Assert.False(ChunkMetadata.TryParse($"h:{MessageId}:0:4:{ChecksumId}:", out var parsed));
        Assert.Null(parsed);
    }
}
