// <copyright file="PerformanceTests.cs">
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
using System.Diagnostics;
using Xunit;

namespace Akri.Connector.SMB.Tests;

/// <summary>
/// Performance benchmark tests for SMB connector.
/// </summary>
public sealed class PerformanceTests : IDisposable
{
    private readonly string _tempDestinationPath;

    public PerformanceTests()
    {
        _tempDestinationPath = Path.Combine(Path.GetTempPath(), $"smb-perf-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDestinationPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDestinationPath))
        {
            try
            {
                Directory.Delete(_tempDestinationPath, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_ListingLargeDirectory_CompletesWithinThreshold()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var optionsMonitor = CreateOptionsMonitor(options);
        var fakeSmbClient = new FakeSMBClient();

        var now = DateTimeOffset.UtcNow;
        
        // Add 1000 files (per SC-002: should list 1,000 files in < 5 seconds)
        for (int i = 0; i < 1000; i++)
        {
            fakeSmbClient.AddFile($"/data{i:D4}.csv", 1024, now.AddHours(-i));
        }

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
        var sw = Stopwatch.StartNew();
        var results = new List<HistorianSample>();
        await foreach (var sample in executor.ExecuteAsync(query, windowStart, windowEnd, CancellationToken.None))
        {
            results.Add(sample);
        }
        sw.Stop();

        // Assert
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5), 
            "listing 1,000 files should complete within 5 seconds (SC-002)");
        results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ProcessingLargeFile_CompletesWithinThreshold()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.MaxFileSizeBytes = 10485760; // 10MB
        var optionsMonitor = CreateOptionsMonitor(options);
        var fakeSmbClient = new FakeSMBClient();

        var now = DateTimeOffset.UtcNow;
        
        // Add a large file (SC-003: 10MB file should process in < 30 seconds)
        fakeSmbClient.AddFile("/large-data.csv", 10485760, now.AddHours(-1));

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
        var sw = Stopwatch.StartNew();
        var results = new List<HistorianSample>();
        await foreach (var sample in executor.ExecuteAsync(query, windowStart, windowEnd, CancellationToken.None))
        {
            results.Add(sample);
        }
        sw.Stop();

        // Assert
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30),
            "processing 10MB file should complete within 30 seconds (SC-003)");
    }

    [Fact]
    public async Task ExecuteAsync_ConnectionEstablishment_CompletesWithinThreshold()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var optionsMonitor = CreateOptionsMonitor(options);
        var fakeSmbClient = new FakeSMBClient();

        var now = DateTimeOffset.UtcNow;
        fakeSmbClient.AddFile("/data.csv", 1024, now.AddHours(-1));

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
        var sw = Stopwatch.StartNew();
        await foreach (var sample in executor.ExecuteAsync(query, windowStart, windowEnd, CancellationToken.None))
        {
            // Just consume samples
        }
        sw.Stop();

        // Assert - SC-001: connection establishment < 10 seconds
        // (in-memory fake is instant, but ensure call overhead is minimal)
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10),
            "connection establishment should complete within 10 seconds (SC-001)");
    }

    [Fact]
    public async Task ExecuteAsync_ConcurrentFileProcessing_HandlesEfficiently()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var optionsMonitor = CreateOptionsMonitor(options);
        var fakeSmbClient = new FakeSMBClient();

        var now = DateTimeOffset.UtcNow;
        
        // Add 100 files to process concurrently
        for (int i = 0; i < 100; i++)
        {
            fakeSmbClient.AddFile($"/data{i}.csv", 10240, now.AddHours(-i));
        }

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
        var sw = Stopwatch.StartNew();
        var results = new List<HistorianSample>();
        await foreach (var sample in executor.ExecuteAsync(query, windowStart, windowEnd, CancellationToken.None))
        {
            results.Add(sample);
        }
        sw.Stop();

        // Assert - should handle multiple files efficiently
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(60),
            "processing 100 files should complete within reasonable time");
        results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_CopyTask_LargeFileTransfer_CompletesWithinThreshold()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.TaskType = "Copy";
        options.DestinationPath = _tempDestinationPath;
        options.MaxFileSizeBytes = 10485760; // 10MB
        var optionsMonitor = CreateOptionsMonitor(options);
        var fakeSmbClient = new FakeSMBClient();

        var now = DateTimeOffset.UtcNow;
        fakeSmbClient.AddFile("/large-doc.csv", 5242880, now.AddHours(-1)); // 5MB

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
        var sw = Stopwatch.StartNew();
        await foreach (var sample in executor.ExecuteAsync(query, windowStart, windowEnd, CancellationToken.None))
        {
            // Copy tasks don't yield samples
        }
        sw.Stop();

        // Assert
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30),
            "copying 5MB file should complete within 30 seconds");
        File.Exists(Path.Combine(_tempDestinationPath, "large-doc.csv")).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WatermarkLookup_IsEfficient()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var optionsMonitor = CreateOptionsMonitor(options);
        var fakeSmbClient = new FakeSMBClient();

        var now = DateTimeOffset.UtcNow;
        fakeSmbClient.AddFile("/data.csv", 1024, now.AddHours(-1));

        var watermarkStore = CreateMockWatermarkStore(now.AddHours(-5));

        var executor = new SMBHistorianExecutor(
            NullLogger<SMBHistorianExecutor>.Instance,
            optionsMonitor,
            fakeSmbClient,
            watermarkStore,
            null);

        var query = CreateTestQuery();
        var windowStart = now.AddDays(-1);
        var windowEnd = now;

        // Act - measure watermark lookup overhead
        var sw = Stopwatch.StartNew();
        await foreach (var sample in executor.ExecuteAsync(query, windowStart, windowEnd, CancellationToken.None))
        {
            // Just consume samples
        }
        sw.Stop();

        // Assert - watermark lookup should add minimal overhead
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5),
            "watermark lookup should be efficient");
    }

    [Fact]
    public async Task ExecuteAsync_CsvParsing_HandlesHighVolumeEfficiently()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var optionsMonitor = CreateOptionsMonitor(options);
        var fakeSmbClient = new FakeSMBClient();

        var now = DateTimeOffset.UtcNow;
        
        // Add 10 files with many samples each
        for (int i = 0; i < 10; i++)
        {
            fakeSmbClient.AddFile($"/data{i}.csv", 102400, now.AddHours(-i)); // ~100KB each
        }

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
        var sw = Stopwatch.StartNew();
        var sampleCount = 0;
        await foreach (var sample in executor.ExecuteAsync(query, windowStart, windowEnd, CancellationToken.None))
        {
            sampleCount++;
        }
        sw.Stop();

        // Assert
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10),
            "CSV parsing of high-volume data should be efficient");
        sampleCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExecuteAsync_MemoryUsage_RemainsReasonable()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var optionsMonitor = CreateOptionsMonitor(options);
        var fakeSmbClient = new FakeSMBClient();

        var now = DateTimeOffset.UtcNow;
        
        // Add 500 files
        for (int i = 0; i < 500; i++)
        {
            fakeSmbClient.AddFile($"/data{i}.csv", 10240, now.AddHours(-i));
        }

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
        var memoryBefore = GC.GetTotalMemory(forceFullCollection: true);
        
        await foreach (var sample in executor.ExecuteAsync(query, windowStart, windowEnd, CancellationToken.None))
        {
            // Process samples
        }

        var memoryAfter = GC.GetTotalMemory(forceFullCollection: false);
        var memoryUsedMB = (memoryAfter - memoryBefore) / (1024.0 * 1024.0);

        // Assert - memory usage should be reasonable (< 100MB for 500 files)
        memoryUsedMB.Should().BeLessThan(100,
            "memory usage should remain reasonable when processing many files");
    }

    [Fact]
    public async Task ValidateAsync_ResponseTime_IsMinimal()
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

        // Act
        var sw = Stopwatch.StartNew();
        var result = await executor.ValidateAsync(query, CancellationToken.None);
        sw.Stop();

        // Assert
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2),
            "validation should be fast");
        result.IsValid.Should().BeTrue();
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
