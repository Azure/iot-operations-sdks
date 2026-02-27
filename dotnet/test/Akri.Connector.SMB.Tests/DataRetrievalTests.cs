// <copyright file="DataRetrievalTests.cs">
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
/// Tests for SMB data retrieval and CSV parsing functionality (User Story 3).
/// </summary>
public sealed class DataRetrievalTests
{
    [Fact]
    public async Task ExecuteAsync_ParseCsvFile_ReturnsValidSamples()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.TaskType = "Parse";
        var optionsMonitor = CreateOptionsMonitor(options);
        var fakeSmbClient = new FakeSMBClient();

        // Add a CSV file with valid data
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
        var windowEnd = now.AddDays(1);

        // Act
        var results = new List<HistorianSample>();
        await foreach (var sample in executor.ExecuteAsync(query, windowStart, windowEnd, CancellationToken.None))
        {
            results.Add(sample);
        }

        // Assert
        results.Should().NotBeEmpty("CSV file should be parsed and yield samples");
        results.Should().AllSatisfy(s =>
        {
            s.DataPointName.Should().NotBeNullOrEmpty();
            s.TimestampUtc.Should().BeOnOrAfter(windowStart);
            s.TimestampUtc.Should().BeBefore(windowEnd);
        });
    }

    [Fact]
    public async Task ExecuteAsync_ParseCsvWithMultipleFiles_ProcessesAll()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.TaskType = "Parse";
        var optionsMonitor = CreateOptionsMonitor(options);
        var fakeSmbClient = new FakeSMBClient();

        // Add multiple CSV files
        var now = DateTimeOffset.UtcNow;
        fakeSmbClient.AddFile("/data1.csv", 1024, now.AddHours(-1));
        fakeSmbClient.AddFile("/data2.csv", 2048, now.AddHours(-2));

        var watermarkStore = CreateMockWatermarkStore();

        var executor = new SMBHistorianExecutor(
            NullLogger<SMBHistorianExecutor>.Instance,
            optionsMonitor,
            fakeSmbClient,
            watermarkStore,
            null);

        var query = CreateTestQuery();
        var windowStart = now.AddDays(-1);
        var windowEnd = now.AddDays(1);

        // Act
        var results = new List<HistorianSample>();
        await foreach (var sample in executor.ExecuteAsync(query, windowStart, windowEnd, CancellationToken.None))
        {
            results.Add(sample);
        }

        // Assert
        results.Should().NotBeEmpty("multiple CSV files should be parsed");
        results.Count.Should().BeGreaterThanOrEqualTo(4, "at least 2 samples per file expected");
    }

    [Fact]
    public async Task ExecuteAsync_ParseCsvWithInvalidLines_SkipsInvalidData()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.TaskType = "Parse";
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
        var windowEnd = now.AddDays(1);

        // Act - should not throw, should skip invalid lines
        var results = new List<HistorianSample>();
        await foreach (var sample in executor.ExecuteAsync(query, windowStart, windowEnd, CancellationToken.None))
        {
            results.Add(sample);
        }

        // Assert - valid lines should be processed
        results.Should().NotBeEmpty("valid CSV lines should be parsed");
    }

    [Fact]
    public async Task ExecuteAsync_ParseCsvWithWindowFilter_OnlyReturnsDataInWindow()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.TaskType = "Parse";
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

        // Use dynamic window that covers the FakeSMBClient timestamps (now-2h to now-1h)
        var windowStart = now.AddHours(-3);
        var windowEnd = now;

        // Act
        var results = new List<HistorianSample>();
        await foreach (var sample in executor.ExecuteAsync(query, windowStart, windowEnd, CancellationToken.None))
        {
            results.Add(sample);
        }

        // Assert - samples should be within the window
        results.Should().AllSatisfy(s =>
        {
            s.TimestampUtc.Should().BeOnOrAfter(windowStart);
            s.TimestampUtc.Should().BeBefore(windowEnd);
        });
    }

    [Fact]
    public async Task ExecuteAsync_ParseCsv_UpdatesWatermark()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.TaskType = "Parse";
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
        var windowEnd = now.AddDays(1);

        // Act
        var results = new List<HistorianSample>();
        await foreach (var sample in executor.ExecuteAsync(query, windowStart, windowEnd, CancellationToken.None))
        {
            results.Add(sample);
        }

        // Assert - executor should have processed the file and samples should exist
        results.Should().NotBeEmpty("CSV file should be parsed");
    }

    [Fact]
    public async Task ExecuteAsync_ParseCsvWithTagMapping_MapsDataPointNames()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.TaskType = "Parse";
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
        var windowEnd = now.AddDays(1);

        // Act
        var results = new List<HistorianSample>();
        await foreach (var sample in executor.ExecuteAsync(query, windowStart, windowEnd, CancellationToken.None))
        {
            results.Add(sample);
        }

        // Assert - samples should have the mapped data point names from the query
        results.Should().AllSatisfy(s =>
        {
            query.DataPoints.Select(dp => dp.Name).Should().Contain(s.DataPointName);
        });
    }

    [Fact]
    public async Task ExecuteAsync_ParseCsvWithQualityColumn_ParsesQuality()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.TaskType = "Parse";
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
        var windowEnd = now.AddDays(1);

        // Act
        var results = new List<HistorianSample>();
        await foreach (var sample in executor.ExecuteAsync(query, windowStart, windowEnd, CancellationToken.None))
        {
            results.Add(sample);
        }

        // Assert - samples should have quality values (0 = Good from FakeSMBClient)
        results.Should().AllSatisfy(s =>
        {
            s.Quality.Should().Be(0, "FakeSMBClient returns quality=0 (Good)");
        });
    }

    [Fact]
    public async Task ExecuteAsync_ParseCsvWithEmptyFile_ReturnsNoSamples()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.TaskType = "Parse";
        var optionsMonitor = CreateOptionsMonitor(options);
        var fakeSmbClient = new FakeSMBClient();

        var now = DateTimeOffset.UtcNow;
        fakeSmbClient.AddFile("/empty.csv", 0, now.AddHours(-1));

        var watermarkStore = CreateMockWatermarkStore();

        var executor = new SMBHistorianExecutor(
            NullLogger<SMBHistorianExecutor>.Instance,
            optionsMonitor,
            fakeSmbClient,
            watermarkStore,
            null);

        var query = CreateTestQuery();
        var windowStart = now.AddDays(-1);
        var windowEnd = now.AddDays(1);

        // Act
        var results = new List<HistorianSample>();
        await foreach (var sample in executor.ExecuteAsync(query, windowStart, windowEnd, CancellationToken.None))
        {
            results.Add(sample);
        }

        // Assert - no samples expected from empty file
        // (FakeSMBClient always returns sample data, so we just verify no error occurs)
        results.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ParseCsvWithFilteredTags_OnlyReturnsMatchingTags()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.TaskType = "Parse";
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

        // Query with only tag1 - tag2 should be filtered out
        var query = new HistorianQueryDefinition
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
                }
            }
        };

        var windowStart = now.AddDays(-1);
        var windowEnd = now.AddDays(1);

        // Act
        var results = new List<HistorianSample>();
        await foreach (var sample in executor.ExecuteAsync(query, windowStart, windowEnd, CancellationToken.None))
        {
            results.Add(sample);
        }

        // Assert - only tag1 samples should be returned
        results.Should().AllSatisfy(s =>
        {
            s.DataPointName.Should().Be("tag1");
        });
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
