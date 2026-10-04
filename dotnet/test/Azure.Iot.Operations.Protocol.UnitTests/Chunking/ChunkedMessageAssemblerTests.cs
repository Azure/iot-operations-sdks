// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Iot.Operations.Protocol.Chunking;
using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;
using Xunit;

namespace Azure.Iot.Operations.Protocol.UnitTests.Chunking
{
    public class ChunkedMessageAssemblerTests
    {
        private const string MessageId = "8ac7a0e4-1b3d-4f9a-9a3f-0d2f6c5b7e11";

        [Fact]
        public void Constructor_SetsProperties_Correctly()
        {
            // Arrange & Act
            var assembler = new ChunkedMessageAssembler(5);

            // Assert
            Assert.False(assembler.IsComplete);
        }

        [Fact]
        public void AddChunk_ReturnsTrueForNewChunk_FalseForDuplicate()
        {
            // Arrange
            var assembler = new ChunkedMessageAssembler(2);
            ChunkMetadata metadata = ChunkMetadata.CreateFirstChunk(MessageId, 2, ChunkChecksums.Sha256.Id, "deadbeef");
            var chunk0 = CreateMqttMessageEventArgs(metadata, string.Empty);

            // Act & Assert
            Assert.True(assembler.AddChunk(metadata, chunk0, out _));
            Assert.False(assembler.AddChunk(metadata, chunk0, out _));
        }

        [Fact]
        public void IsComplete_ReturnsTrueWhenAllChunksReceived()
        {
            // Arrange
            var assembler = new ChunkedMessageAssembler(2);
            ChunkMetadata headMetadata = ChunkMetadata.CreateFirstChunk(MessageId, 2, ChunkChecksums.Sha256.Id, "deadbeef");
            ChunkMetadata dataMetadata = ChunkMetadata.CreateDataChunk(MessageId, 1, 2);
            var chunk0 = CreateMqttMessageEventArgs(headMetadata, string.Empty);
            var chunk1 = CreateMqttMessageEventArgs(dataMetadata, "payload2");

            // Act
            assembler.AddChunk(headMetadata, chunk0, out _);
            assembler.AddChunk(dataMetadata, chunk1, out _);

            // Assert
            Assert.True(assembler.IsComplete);
        }

        [Fact]
        public void TryReassemble_ReturnsFalseWhenNotComplete()
        {
            // Arrange
            var assembler = new ChunkedMessageAssembler(2);
            ChunkMetadata metadata = ChunkMetadata.CreateFirstChunk(MessageId, 2, ChunkChecksums.Sha256.Id, "deadbeef");
            var chunk0 = CreateMqttMessageEventArgs(metadata, string.Empty);

            // Act
            assembler.AddChunk(metadata, chunk0, out _);
            var result = assembler.TryReassemble(out var reassembledArgs);

            // Assert
            Assert.False(result);
            Assert.Null(reassembledArgs);
        }

        [Fact]
        public void TryReassemble_ReturnsValidMessageWhenComplete()
        {
            // Arrange
            var assembler = new ChunkedMessageAssembler(3);
            ChunkMetadata headMetadata = ChunkMetadata.CreateFirstChunk(MessageId, 3, ChunkChecksums.Sha256.Id, "deadbeef");
            ChunkMetadata data1Metadata = ChunkMetadata.CreateDataChunk(MessageId, 1, 3);
            ChunkMetadata data2Metadata = ChunkMetadata.CreateDataChunk(MessageId, 2, 3);
            var chunk0 = CreateMqttMessageEventArgs(headMetadata, string.Empty);
            var chunk1 = CreateMqttMessageEventArgs(data1Metadata, "payload1");
            var chunk2 = CreateMqttMessageEventArgs(data2Metadata, " payload2");

            // Act
            assembler.AddChunk(headMetadata, chunk0, out _);
            assembler.AddChunk(data1Metadata, chunk1, out _);
            assembler.AddChunk(data2Metadata, chunk2, out _);
            var result = assembler.TryReassemble(out var reassembledArgs);

            // Assert
            Assert.True(result);
            Assert.NotNull(reassembledArgs);

            // Convert payload to string for easier assertion
            var payload = reassembledArgs!.ApplicationMessage.Payload;
            var assembledPayloadAsString = "";
            foreach (var segment in payload)
            {
                assembledPayloadAsString += Encoding.UTF8.GetString(segment.Span);
            }

            Assert.Equal("payload1 payload2", assembledPayloadAsString);
        }

        [Fact]
        public void TryReassemble_ChecksumVerification_Success()
        {
            // Arrange
            var payload1 = "payload1";
            var payload2 = "payload2";
            var combined = payload1 + payload2;
            var combinedBytes = Encoding.UTF8.GetBytes(combined);
            var ros = new ReadOnlySequence<byte>(combinedBytes);

            // Calculate the actual checksum
            var checksum = ChunkChecksums.Sha256.Compute(ros);

            var assembler = new ChunkedMessageAssembler(3);
            ChunkMetadata headMetadata = ChunkMetadata.CreateFirstChunk(MessageId, 3, ChunkChecksums.Sha256.Id, checksum);
            ChunkMetadata data1Metadata = ChunkMetadata.CreateDataChunk(MessageId, 1, 3);
            ChunkMetadata data2Metadata = ChunkMetadata.CreateDataChunk(MessageId, 2, 3);
            Assert.True(assembler.TryUpdateMetadata(3, ChunkChecksums.Sha256.Id, checksum, ChunkChecksums.Sha256));

            var chunk0 = CreateMqttMessageEventArgs(headMetadata, string.Empty);
            var chunk1 = CreateMqttMessageEventArgs(data1Metadata, payload1);
            var chunk2 = CreateMqttMessageEventArgs(data2Metadata, payload2);

            // Act
            assembler.AddChunk(headMetadata, chunk0, out _);
            assembler.AddChunk(data1Metadata, chunk1, out _);
            assembler.AddChunk(data2Metadata, chunk2, out _);
            var result = assembler.TryReassemble(out var reassembledArgs);

            // Assert
            Assert.True(result);
            Assert.NotNull(reassembledArgs);
        }

        [Fact]
        public void TryReassemble_ChecksumVerification_Failure()
        {
            // Arrange
            var assembler = new ChunkedMessageAssembler(3);
            const string checksum = "deadbeef";
            ChunkMetadata headMetadata = ChunkMetadata.CreateFirstChunk(MessageId, 3, ChunkChecksums.Sha256.Id, checksum);
            ChunkMetadata data1Metadata = ChunkMetadata.CreateDataChunk(MessageId, 1, 3);
            ChunkMetadata data2Metadata = ChunkMetadata.CreateDataChunk(MessageId, 2, 3);
            Assert.True(assembler.TryUpdateMetadata(3, ChunkChecksums.Sha256.Id, checksum, ChunkChecksums.Sha256));

            var chunk0 = CreateMqttMessageEventArgs(headMetadata, string.Empty);
            var chunk1 = CreateMqttMessageEventArgs(data1Metadata, "payload1");
            var chunk2 = CreateMqttMessageEventArgs(data2Metadata, "payload2");

            // Act
            assembler.AddChunk(headMetadata, chunk0, out _);
            assembler.AddChunk(data1Metadata, chunk1, out _);
            assembler.AddChunk(data2Metadata, chunk2, out _);
            var result = assembler.TryReassemble(out var reassembledArgs);

            // Assert
            Assert.False(result);
            Assert.Null(reassembledArgs);
        }

        [Fact]
        public void TryReassemble_ChecksumThrowsOutOfMemoryException_Rethrows()
        {
            var checksumAlgorithm = new OutOfMemoryChecksum();
            var assembler = new ChunkedMessageAssembler(2);
            ChunkMetadata headMetadata = ChunkMetadata.CreateFirstChunk(MessageId, 2, checksumAlgorithm.Id, "deadbeef");
            ChunkMetadata dataMetadata = ChunkMetadata.CreateDataChunk(MessageId, 1, 2);
            Assert.True(assembler.TryUpdateMetadata(2, checksumAlgorithm.Id, "deadbeef", checksumAlgorithm));
            assembler.AddChunk(headMetadata, CreateMqttMessageEventArgs(headMetadata, string.Empty), out _);
            assembler.AddChunk(dataMetadata, CreateMqttMessageEventArgs(dataMetadata, "payload"), out _);

            Assert.Throws<OutOfMemoryException>(() => assembler.TryReassemble(out _));
        }

        [Fact]
        public async Task AcknowledgeHandler_Calls_AcknowledgeAsync_On_All_Chunks()
        {
            // Arrange
            var assembler = new ChunkedMessageAssembler(2);
            ChunkMetadata headMetadata = ChunkMetadata.CreateFirstChunk(MessageId, 2, ChunkChecksums.Sha256.Id, "deadbeef");
            ChunkMetadata dataMetadata = ChunkMetadata.CreateDataChunk(MessageId, 1, 2);
            var chunk0AckCount = false;
            var chunk1AckCount = false;

            // Create mock message args with mock acknowledgeAsync methods
            var chunk0 = CreateMqttMessageEventArgsWithAckHandler(headMetadata, string.Empty, (_, _) =>
            {
                chunk0AckCount = true;
                return Task.CompletedTask;
            });
            var chunk1 = CreateMqttMessageEventArgsWithAckHandler(dataMetadata, "testpayload", (_, _) =>
            {
                chunk1AckCount = true;
                return Task.CompletedTask;
            });

            // Act
            assembler.AddChunk(headMetadata, chunk0, out _);
            assembler.AddChunk(dataMetadata, chunk1, out _);
            var result = assembler.TryReassemble(out var reassembledArgs);

            // Simulate acknowledgment of reassembled message
            if (reassembledArgs != null)
            {
                await reassembledArgs.AcknowledgeAsync(CancellationToken.None);
            }

            // Assert
            Assert.True(result);
            Assert.True(chunk0AckCount);
            Assert.True(chunk1AckCount);
        }

        // Helper method to create a simple MQTT message event args with payload
        private static MqttApplicationMessageReceivedEventArgs CreateMqttMessageEventArgs(
            ChunkMetadata metadata,
            string payload)
        {
            var bytes = Encoding.UTF8.GetBytes(payload);
            var mqttMessage = new MqttApplicationMessage("test/topic")
            {
                Payload = new ReadOnlySequence<byte>(bytes),
                UserProperties = [new MqttUserProperty(ChunkingConstants.ChunkUserProperty, metadata.Format())],
            };

            return new MqttApplicationMessageReceivedEventArgs(
                "client1",
                mqttMessage,
                1,
                (_, _) => Task.CompletedTask);
        }

        // Helper method to create a mock MQTT message event args
        private static MqttApplicationMessageReceivedEventArgs CreateMqttMessageEventArgsWithAckHandler(
            ChunkMetadata metadata,
            string payload,
            Func<MqttApplicationMessageReceivedEventArgs, CancellationToken, Task> acknowledgeHandler)
        {
            var bytes = Encoding.UTF8.GetBytes(payload);
            var mqttMessage = new MqttApplicationMessage("test/topic")
            {
                Payload = new ReadOnlySequence<byte>(bytes),
                UserProperties = [new MqttUserProperty(ChunkingConstants.ChunkUserProperty, metadata.Format())],
            };

            var messageEventArgs = new MqttApplicationMessageReceivedEventArgs(
                "client1",
                mqttMessage,
                1,
                acknowledgeHandler);

            return messageEventArgs;
        }

        private sealed class OutOfMemoryChecksum : IChunkChecksum
        {
            public string Id => "oom";

            public string Compute(ReadOnlySequence<byte> payload) => throw new OutOfMemoryException();
        }
    }
}
