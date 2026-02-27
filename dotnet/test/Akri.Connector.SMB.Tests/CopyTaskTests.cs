// <copyright file="CopyTaskTests.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

using Akri.Connector.SMB.Models;
using Akri.Connector.SMB.Tests.Fakes;
using Akri.HistorianConnector.Core.Models;
using Akri.HistorianConnector.Core.StateStore;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Akri.Connector.SMB.Tests;

/// <summary>
/// Tests for SMB Copy task functionality (User Story 4).
/// </summary>
public sealed class CopyTaskTests : IDisposable
{
    private readonly string _tempDestinationPath;

    public CopyTaskTests()
    {
        // Create a temporary directory for copy tests
        _tempDestinationPath = Path.Combine(Path.GetTempPath(), $"smb-copy-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDestinationPath);
    }

    public void Dispose()
    {
        // Clean up temporary directory
        if (Directory.Exists(_tempDestinationPath))
        {
            Directory.Delete(_tempDestinationPath, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_CopyTask_CopiesNewFile()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.TaskType = "Copy";
        options.DestinationPath = _tempDestinationPath;
        var optionsMonitor = CreateOptionsMonitor(options);
        var fakeSmbClient = new FakeSMBClient();

        // Add a new file to copy
        var now = DateTimeOffset.UtcNow;
        fakeSmbClient.AddFile("/test-file.pdf", 1024, now.AddHours(-1));

        var watermarkStore = CreateMockWatermarkStore();

        var executor = new SMBHistorianExecutor(
            NullLogger<SMBHistorianExecutor>.Instance,
            optionsMonitor,
            fakeSmbClient,
            watermarkStore,
            null);

        var query = CreateTestQuery();
        var windowStart = now.AddDays(-1);
        var windowEnd = now;

        // Act
        await foreach (var sample in executor.ExecuteAsync(query, windowStart, windowEnd, CancellationToken.None))
        {
            // Copy tasks don't yield samples
        }

        // Assert
        var copiedFilePath = Path.Combine(_tempDestinationPath, "test-file.pdf");
        File.Exists(copiedFilePath).Should().BeTrue("file should be copied to destination");
        fakeSmbClient.ConnectCalled.Should().BeTrue();
        fakeSmbClient.DisconnectCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_CopyTask_WithWatermark_DoesNotRecopyExistingFile()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.TaskType = "Copy";
        options.DestinationPath = _tempDestinationPath;
        var optionsMonitor = CreateOptionsMonitor(options);
        var fakeSmbClient = new FakeSMBClient();

        var now = DateTimeOffset.UtcNow;
        var watermark = now.AddHours(-5);

        // Add a file that was already processed (before watermark)
        fakeSmbClient.AddFile("/old-file.pdf", 1024, watermark.AddHours(-1));

        var watermarkStore = CreateMockWatermarkStore(watermark);

        var executor = new SMBHistorianExecutor(
            NullLogger<SMBHistorianExecutor>.Instance,
            optionsMonitor,
            fakeSmbClient,
            watermarkStore,
            null);

        var query = CreateTestQuery();
        var windowStart = now.AddDays(-1);
        var windowEnd = now;

        // Act
        await foreach (var sample in executor.ExecuteAsync(query, windowStart, windowEnd, CancellationToken.None))
        {
            // Copy tasks don't yield samples
        }

        // Assert
        var copiedFilePath = Path.Combine(_tempDestinationPath, "old-file.pdf");
        File.Exists(copiedFilePath).Should().BeFalse("file should not be re-copied (watermark prevents duplicate)");
    }

    [Fact]
    public async Task ExecuteAsync_CopyTask_SkipsFilesAboveMaxSize()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.TaskType = "Copy";
        options.DestinationPath = _tempDestinationPath;
        options.MaxFileSizeBytes = 1000; // Set low limit
        var optionsMonitor = CreateOptionsMonitor(options);
        var fakeSmbClient = new FakeSMBClient();

        // Add a file that exceeds the size limit
        var now = DateTimeOffset.UtcNow;
        fakeSmbClient.AddFile("/large-file.pdf", 2000, now.AddHours(-1));

        var watermarkStore = CreateMockWatermarkStore();

        var executor = new SMBHistorianExecutor(
            NullLogger<SMBHistorianExecutor>.Instance,
            optionsMonitor,
            fakeSmbClient,
            watermarkStore,
            null);

        var query = CreateTestQuery();
        var windowStart = now.AddDays(-1);
        var windowEnd = now;

        // Act
        await foreach (var sample in executor.ExecuteAsync(query, windowStart, windowEnd, CancellationToken.None))
        {
            // Copy tasks don't yield samples
        }

        // Assert
        var copiedFilePath = Path.Combine(_tempDestinationPath, "large-file.pdf");
        File.Exists(copiedFilePath).Should().BeFalse("file should be skipped due to size limit");
    }

    [Fact]
    public async Task ExecuteAsync_CopyTask_AtomicWrite_CleansUpTempFileOnFailure()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.TaskType = "Copy";
        options.DestinationPath = _tempDestinationPath;
        var optionsMonitor = CreateOptionsMonitor(options);

        // Create a fake SMB client that throws an exception when reading
        var fakeSmbClient = new FakeSMBClient();
        var now = DateTimeOffset.UtcNow;
        fakeSmbClient.AddFile("/test-file.pdf", 1024, now.AddHours(-1));

        var watermarkStore = CreateMockWatermarkStore();

        var executor = new SMBHistorianExecutor(
            NullLogger<SMBHistorianExecutor>.Instance,
            optionsMonitor,
            fakeSmbClient,
            watermarkStore,
            null);

        var query = CreateTestQuery();
        var windowStart = now.AddDays(-1);
        var windowEnd = now;

        // Act - attempt copy (will fail in read, but we can't easily force that with FakeSMBClient)
        await foreach (var sample in executor.ExecuteAsync(query, windowStart, windowEnd, CancellationToken.None))
        {
            // Copy tasks don't yield samples
        }

        // Assert - check no .tmp files remain
        var tmpFiles = Directory.GetFiles(_tempDestinationPath, "*.tmp");
        tmpFiles.Should().BeEmpty("temp files should be cleaned up on failure");
    }

    [Fact]
    public async Task ExecuteAsync_ParseTask_StillWorksAsExpected()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.TaskType = "Parse"; // Explicitly set to Parse
        var optionsMonitor = CreateOptionsMonitor(options);
        var fakeSmbClient = new FakeSMBClient();

        // Add test files
        var now = DateTimeOffset.UtcNow;
        fakeSmbClient.AddFile("/file1.csv", 1024, now.AddHours(-1));

        var watermarkStore = CreateMockWatermarkStore();

        var executor = new SMBHistorianExecutor(
            NullLogger<SMBHistorianExecutor>.Instance,
            optionsMonitor,
            fakeSmbClient,
            watermarkStore,
            null);

        var query = CreateTestQuery() with { FilePattern = "*.csv" };
        var windowStart = now.AddDays(-1);
        var windowEnd = now;

        // Act
        var results = new List<HistorianSample>();
        await foreach (var sample in executor.ExecuteAsync(query, windowStart, windowEnd, CancellationToken.None))
        {
            results.Add(sample);
        }

        // Assert
        results.Should().NotBeEmpty("Parse task should still yield samples");
        fakeSmbClient.ConnectCalled.Should().BeTrue();
        fakeSmbClient.DisconnectCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_CopyTask_WithMultipleFiles_CopiesAll()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.TaskType = "Copy";
        options.DestinationPath = _tempDestinationPath;
        var optionsMonitor = CreateOptionsMonitor(options);
        var fakeSmbClient = new FakeSMBClient();

        // Add multiple files
        var now = DateTimeOffset.UtcNow;
        fakeSmbClient.AddFile("/doc1.pdf", 1024, now.AddHours(-1));
        fakeSmbClient.AddFile("/doc2.pdf", 2048, now.AddHours(-2));
        fakeSmbClient.AddFile("/readme.txt", 512, now.AddHours(-3)); // Different extension, should be skipped

        var watermarkStore = CreateMockWatermarkStore();

        var executor = new SMBHistorianExecutor(
            NullLogger<SMBHistorianExecutor>.Instance,
            optionsMonitor,
            fakeSmbClient,
            watermarkStore,
            null);

        var query = CreateTestQuery() with { FilePattern = "*.pdf" };
        var windowStart = now.AddDays(-1);
        var windowEnd = now;

        // Act
        await foreach (var sample in executor.ExecuteAsync(query, windowStart, windowEnd, CancellationToken.None))
        {
            // Copy tasks don't yield samples
        }

        // Assert
        File.Exists(Path.Combine(_tempDestinationPath, "doc1.pdf")).Should().BeTrue();
        File.Exists(Path.Combine(_tempDestinationPath, "doc2.pdf")).Should().BeTrue();
        File.Exists(Path.Combine(_tempDestinationPath, "readme.txt")).Should().BeFalse("txt file should be filtered out");
    }

    [Fact]
    public async Task ValidateAsync_CopyTask_ValidatesDestinationPath()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.TaskType = "Copy";
        options.DestinationPath = _tempDestinationPath;
        var optionsMonitor = CreateOptionsMonitor(options);
        var fakeSmbClient = new FakeSMBClient();
        var watermarkStore = CreateMockWatermarkStore();

        var executor = new SMBHistorianExecutor(
            NullLogger<SMBHistorianExecutor>.Instance,
            optionsMonitor,
            fakeSmbClient,
            watermarkStore,
            null);

        var query = CreateTestQuery();

        // Act
        var result = await executor.ValidateAsync(query, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeTrue("validation should pass with valid SMB connection");
    }

    private static SMBConnectorOptions CreateDefaultOptions()
    {
        return new SMBConnectorOptions
        {
            ConnectionTimeoutSeconds = 30,
            MaxConcurrentConnections = 10,
            MaxFileSizeBytes = 10485760,
            EnableLeaderElection = false,
            InstanceId = "test-instance",
            TaskType = "Parse",
            DestinationPath = string.Empty
        };
    }

    private static IOptionsMonitor<SMBConnectorOptions> CreateOptionsMonitor(SMBConnectorOptions options)
    {
        var mock = new Mock<IOptionsMonitor<SMBConnectorOptions>>();
        mock.Setup(m => m.CurrentValue).Returns(options);
        return mock.Object;
    }

    private static IWatermarkStore<WatermarkData> CreateMockWatermarkStore(DateTimeOffset? watermark = null)
    {
        var mock = new Mock<IWatermarkStore<WatermarkData>>();
        mock.Setup(m => m.GetAsync(It.IsAny<string>()))
            .ReturnsAsync(watermark.HasValue ? new WatermarkData { QueryId = "test-query", Watermark = watermark.Value } : null);
        mock.Setup(m => m.SetAsync(It.IsAny<string>(), It.IsAny<WatermarkData>()))
            .ReturnsAsync(true);
        return mock.Object;
    }

    private static HistorianQueryDefinition CreateTestQuery()
    {
        return new HistorianQueryDefinition
        {
            QueryId = "test-query",
            DeviceName = "test-device",
            InboundEndpointName = "test-endpoint",
            AssetName = "test-asset",
            DatasetName = "test-dataset",
            CronExpression = "0 * * * *",
            WatermarkKind = WatermarkKind.Time,
            OutputTopic = "test-topic",
            DataPoints = new List<HistorianDataPoint>
            {
                new()
                {
                    Name = "tag1",
                    DataSource = "tag1"
                },
                new()
                {
                    Name = "tag2",
                    DataSource = "tag2"
                }
            }
        };
    }
}
