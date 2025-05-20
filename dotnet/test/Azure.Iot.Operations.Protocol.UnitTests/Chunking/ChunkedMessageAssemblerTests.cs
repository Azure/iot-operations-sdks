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
using Moq;
using Xunit;

namespace Azure.Iot.Operations.Protocol.UnitTests.Chunking
{
    public class ChunkedMessageAssemblerTests
    {
        [Fact]
        public void Constructor_SetsProperties_Correctly()
        {
            // Arrange & Act
            var assembler = new ChunkedMessageAssembler(5, ChunkingChecksumAlgorithm.SHA256);

            // Assert
            Assert.False(assembler.IsComplete);
        }

        [Fact]
        public void AddChunk_ReturnsTrueForNewChunk_FalseForDuplicate()
        {
            // Arrange
            var assembler = new ChunkedMessageAssembler(2, ChunkingChecksumAlgorithm.SHA256);
            var chunk0 = CreateMqttMessageEventArgs("payload1");

            // Act & Assert
            Assert.True(assembler.AddChunk(0, chunk0)); // First time should return true
            Assert.False(assembler.AddChunk(0, chunk0)); // Second time should return false (duplicate)
        }

        [Fact]
        public void IsComplete_ReturnsTrueWhenAllChunksReceived()
        {
            // Arrange
            var assembler = new ChunkedMessageAssembler(2, ChunkingChecksumAlgorithm.SHA256);
            var chunk0 = CreateMqttMessageEventArgs("payload1");
            var chunk1 = CreateMqttMessageEventArgs("payload2");

            // Act
            assembler.AddChunk(0, chunk0);
            assembler.AddChunk(1, chunk1);

            // Assert
            Assert.True(assembler.IsComplete);
        }

        [Fact]
        public void TryReassemble_ReturnsFalseWhenNotComplete()
        {
            // Arrange
            var assembler = new ChunkedMessageAssembler(2, ChunkingChecksumAlgorithm.SHA256);
            var chunk0 = CreateMqttMessageEventArgs("payload1");

            // Act
            assembler.AddChunk(0, chunk0);
            var result = assembler.TryReassemble(out var reassembledArgs);

            // Assert
            Assert.False(result);
            Assert.Null(reassembledArgs);
        }

        [Fact]
        public void TryReassemble_ReturnsValidMessageWhenComplete()
        {
            // Arrange
            var assembler = new ChunkedMessageAssembler(2, ChunkingChecksumAlgorithm.SHA256);
            var chunk0 = CreateMqttMessageEventArgs("payload1");
            var chunk1 = CreateMqttMessageEventArgs("payload2");

            // Act
            assembler.AddChunk(0, chunk0);
            assembler.AddChunk(1, chunk1);
            var result = assembler.TryReassemble(out var reassembledArgs);

            // Assert
            Assert.True(result);
            Assert.NotNull(reassembledArgs);

            // Convert payload to string for easier assertion
            var payload = reassembledArgs!.ApplicationMessage.Payload;
            var combined = "";
            foreach (var segment in payload)
            {
                combined += Encoding.UTF8.GetString(segment.Span);
            }

            Assert.Equal("payload1payload2", combined);
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
            var checksum = ChecksumCalculator.CalculateChecksum(ros, ChunkingChecksumAlgorithm.SHA256);

            var assembler = new ChunkedMessageAssembler(2, ChunkingChecksumAlgorithm.SHA256);
            assembler.UpdateMetadata(2, checksum); // Set the correct checksum

            var chunk0 = CreateMqttMessageEventArgs(payload1);
            var chunk1 = CreateMqttMessageEventArgs(payload2);

            // Act
            assembler.AddChunk(0, chunk0);
            assembler.AddChunk(1, chunk1);
            var result = assembler.TryReassemble(out var reassembledArgs);

            // Assert
            Assert.True(result);
            Assert.NotNull(reassembledArgs);
        }

        [Fact]
        public void TryReassemble_ChecksumVerification_Failure()
        {
            // Arrange
            var assembler = new ChunkedMessageAssembler(2, ChunkingChecksumAlgorithm.SHA256);
            assembler.UpdateMetadata(2, "invalid-checksum"); // Set incorrect checksum

            var chunk0 = CreateMqttMessageEventArgs("payload1");
            var chunk1 = CreateMqttMessageEventArgs("payload2");

            // Act
            assembler.AddChunk(0, chunk0);
            assembler.AddChunk(1, chunk1);
            var result = assembler.TryReassemble(out var reassembledArgs);

            // Assert
            Assert.False(result);
            Assert.Null(reassembledArgs);
        }

        [Fact]
        public void HasExpired_ReturnsTrueWhenTimeoutExceeded()
        {
            // Arrange
            var assembler = new ChunkedMessageAssembler(2, ChunkingChecksumAlgorithm.SHA256);
            var shortTimeout = TimeSpan.FromMilliseconds(1);

            // Act
            Thread.Sleep(10); // Ensure timeout is exceeded
            var result = assembler.HasExpired(shortTimeout);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void HasExpired_ReturnsFalseWhenTimeoutNotExceeded()
        {
            // Arrange
            var assembler = new ChunkedMessageAssembler(2, ChunkingChecksumAlgorithm.SHA256);
            var longTimeout = TimeSpan.FromMinutes(5);

            // Act
            var result = assembler.HasExpired(longTimeout);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task AcknowledgeHandler_Calls_AcknowledgeAsync_On_All_Chunks()
        {
            // Arrange
            var assembler = new ChunkedMessageAssembler(2, ChunkingChecksumAlgorithm.SHA256);
            var chunk0AckCount = false;
            var chunk1AckCount = false;

            // Create mock message args with mock acknowledgeAsync methods
            var chunk0 = CreateMqttMessageEventArgsWithAckHandler((_, _) =>
            {
                chunk0AckCount = true;
                return Task.CompletedTask;
            });
            var chunk1 = CreateMqttMessageEventArgsWithAckHandler((_, _) =>
            {
                chunk1AckCount = true;
                return Task.CompletedTask;
            });

            // Act
            assembler.AddChunk(0, chunk0);
            assembler.AddChunk(1, chunk1);
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
        private static MqttApplicationMessageReceivedEventArgs CreateMqttMessageEventArgs(string payload)
        {
            var bytes = Encoding.UTF8.GetBytes(payload);
            var mqttMessage = new MqttApplicationMessage("test/topic")
            {
                Payload = new ReadOnlySequence<byte>(bytes)
            };

            return new MqttApplicationMessageReceivedEventArgs(
                "client1",
                mqttMessage,
                1,
                (_, _) => Task.CompletedTask);
        }

        // Helper method to create a mock MQTT message event args
        private static MqttApplicationMessageReceivedEventArgs CreateMqttMessageEventArgsWithAckHandler(Func<MqttApplicationMessageReceivedEventArgs, CancellationToken, Task> acknowledgeHandler)
        {
            var bytes = "testpayload"u8.ToArray();
            var mqttMessage = new MqttApplicationMessage("test/topic")
            {
                Payload = new ReadOnlySequence<byte>(bytes)
            };

            var messageEventArgs = new MqttApplicationMessageReceivedEventArgs(
                "client1",
                mqttMessage,
                1,
                acknowledgeHandler);

            return messageEventArgs;
        }
    }
}
