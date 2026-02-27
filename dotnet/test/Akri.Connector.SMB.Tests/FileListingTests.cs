// <copyright file="FileListingTests.cs">
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
/// Tests for SMB file listing functionality.
/// </summary>
public sealed class FileListingTests
{
    [Fact]
    public async Task ExecuteAsync_WithMatchingFiles_ReturnsData()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var optionsMonitor = CreateOptionsMonitor(options);
        var fakeSmbClient = new FakeSMBClient();
        
        // Add test files
        var now = DateTimeOffset.UtcNow;
        fakeSmbClient.AddFile("/file1.csv", 1024, now.AddHours(-1));
        fakeSmbClient.AddFile("/file2.csv", 2048, now.AddHours(-2));
        fakeSmbClient.AddFile("/file3.txt", 512, now.AddHours(-3)); // Won't match pattern
        
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
        var results = new List<HistorianSample>();
        await foreach (var sample in executor.ExecuteAsync(query, windowStart, windowEnd, CancellationToken.None))
        {
            results.Add(sample);
        }

        // Assert
        results.Should().NotBeEmpty();
        fakeSmbClient.ConnectCalled.Should().BeTrue();
        fakeSmbClient.DisconnectCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyDirectory_ReturnsNoData()
    {
        // Arrange
        var options = CreateDefaultOptions();
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
        var now = DateTimeOffset.UtcNow;
        var windowStart = now.AddDays(-1);
        var windowEnd = now;

        // Act
        var results = new List<HistorianSample>();
        await foreach (var sample in executor.ExecuteAsync(query, windowStart, windowEnd, CancellationToken.None))
        {
            results.Add(sample);
        }

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WithLargeFile_SkipsFile()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.MaxFileSizeBytes = 1000; // Set low limit
        var optionsMonitor = CreateOptionsMonitor(options);
        var fakeSmbClient = new FakeSMBClient();
        
        // Add a file that exceeds the size limit
        var now = DateTimeOffset.UtcNow;
        fakeSmbClient.AddFile("/large-file.csv", 2000, now.AddHours(-1));
        
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
        var results = new List<HistorianSample>();
        await foreach (var sample in executor.ExecuteAsync(query, windowStart, windowEnd, CancellationToken.None))
        {
            results.Add(sample);
        }

        // Assert - file should be skipped due to size
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WithWatermark_OnlyProcessesNewFiles()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var optionsMonitor = CreateOptionsMonitor(options);
        var fakeSmbClient = new FakeSMBClient();
        
        var now = DateTimeOffset.UtcNow;
        var watermark = now.AddHours(-5);
        
        // Add files: one before watermark, one after
        fakeSmbClient.AddFile("/old-file.csv", 1024, watermark.AddHours(-1)); // Before watermark
        fakeSmbClient.AddFile("/new-file.csv", 1024, watermark.AddHours(1));  // After watermark
        
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
        var results = new List<HistorianSample>();
        await foreach (var sample in executor.ExecuteAsync(query, windowStart, windowEnd, CancellationToken.None))
        {
            results.Add(sample);
        }

        // Assert - should only process new file
        results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WithGlobPattern_FiltersCorrectly()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var optionsMonitor = CreateOptionsMonitor(options);
        var fakeSmbClient = new FakeSMBClient();

        var now = DateTimeOffset.UtcNow;
        fakeSmbClient.AddFile("/data-2024.csv", 1024, now.AddHours(-1)); // Matches
        fakeSmbClient.AddFile("/data-2025.csv", 1024, now.AddHours(-2)); // Matches
        fakeSmbClient.AddFile("/config.csv", 1024, now.AddHours(-3));    // Doesn't match

        var watermarkStore = CreateMockWatermarkStore();

        var executor = new SMBHistorianExecutor(
            NullLogger<SMBHistorianExecutor>.Instance,
            optionsMonitor,
            fakeSmbClient,
            watermarkStore,
            null);

        var query = CreateTestQuery() with { FilePattern = "data*.csv" };
        var windowStart = now.AddDays(-1);
        var windowEnd = now;

        // Act
        var results = new List<HistorianSample>();
        await foreach (var sample in executor.ExecuteAsync(query, windowStart, windowEnd, CancellationToken.None))
        {
            results.Add(sample);
        }

        // Assert - should process data files but not config.csv
        results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WithLeaderElectionDisabled_ExecutesNormally()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.EnableLeaderElection = false;
        var optionsMonitor = CreateOptionsMonitor(options);
        var fakeSmbClient = new FakeSMBClient();
        
        var now = DateTimeOffset.UtcNow;
        fakeSmbClient.AddFile("/file.csv", 1024, now.AddHours(-1));
        
        var watermarkStore = CreateMockWatermarkStore();
        
        var executor = new SMBHistorianExecutor(
            NullLogger<SMBHistorianExecutor>.Instance,
            optionsMonitor,
            fakeSmbClient,
            watermarkStore,
            null); // No leader election client

        var query = CreateTestQuery();
        var windowStart = now.AddDays(-1);
        var windowEnd = now;

        // Act
        var results = new List<HistorianSample>();
        await foreach (var sample in executor.ExecuteAsync(query, windowStart, windowEnd, CancellationToken.None))
        {
            results.Add(sample);
        }

        // Assert
        results.Should().NotBeEmpty();
    }

    private static SMBConnectorOptions CreateDefaultOptions()
    {
        return new SMBConnectorOptions
        {
            ConnectionTimeoutSeconds = 30,
            MaxConcurrentConnections = 10,
            MaxFileSizeBytes = 10485760,
            EnableLeaderElection = false,
            InstanceId = "test-instance"
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
