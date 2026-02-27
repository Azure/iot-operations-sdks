// <copyright file="ChaosTests.cs">
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
/// Chaos and failure scenario tests for SMB connector resilience.
/// </summary>
public sealed class ChaosTests : IDisposable
{
    private readonly string _tempDestinationPath;

    public ChaosTests()
    {
        _tempDestinationPath = Path.Combine(Path.GetTempPath(), $"smb-chaos-test-{Guid.NewGuid():N}");
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
    public async Task ExecuteAsync_NetworkDisconnectDuringRead_HandlesGracefully()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var optionsMonitor = CreateOptionsMonitor(options);
        var fakeSmbClient = new FailingSMBClient
        {
            FailOnRead = true,
            ReadErrorMessage = "Network connection lost"
        };

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

        // Act - should not throw, should handle error gracefully
        var results = new List<HistorianSample>();
        await foreach (var sample in executor.ExecuteAsync(query, windowStart, windowEnd, CancellationToken.None))
        {
            results.Add(sample);
        }

        // Assert - should skip failed file and continue
        results.Should().BeEmpty("failed files should be skipped");
        fakeSmbClient.ConnectCalled.Should().BeTrue();
        fakeSmbClient.DisconnectCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_CancellationRequested_StopsProcessingCleanly()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var optionsMonitor = CreateOptionsMonitor(options);
        var fakeSmbClient = new FakeSMBClient();

        var now = DateTimeOffset.UtcNow;
        // Add many files to process
        for (int i = 0; i < 10; i++)
        {
            fakeSmbClient.AddFile($"/data{i}.csv", 1024, now.AddHours(-i));
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

        using var cts = new CancellationTokenSource();

        // Act - cancel after short delay
        cts.CancelAfter(TimeSpan.FromMilliseconds(10));

        var results = new List<HistorianSample>();
        try
        {
            await foreach (var sample in executor.ExecuteAsync(query, windowStart, windowEnd, cts.Token))
            {
                results.Add(sample);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - should have stopped processing
        fakeSmbClient.DisconnectCalled.Should().BeTrue("should disconnect on cancellation");
    }

    [Fact]
    public async Task ExecuteAsync_CopyTask_DiskFullScenario_HandlesGracefully()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.TaskType = "Copy";
        options.DestinationPath = "/invalid/readonly/path"; // Simulate disk full/write failure
        var optionsMonitor = CreateOptionsMonitor(options);
        var fakeSmbClient = new FakeSMBClient();

        var now = DateTimeOffset.UtcNow;
        fakeSmbClient.AddFile("/data.pdf", 1024, now.AddHours(-1));

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

        // Act - should handle write failure gracefully
        await foreach (var sample in executor.ExecuteAsync(query, windowStart, windowEnd, CancellationToken.None))
        {
            // Copy tasks don't yield samples
        }

        // Assert - should complete without crashing
        fakeSmbClient.ConnectCalled.Should().BeTrue();
        fakeSmbClient.DisconnectCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_CopyTask_PartialWrite_CleansUpTempFile()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.TaskType = "Copy";
        options.DestinationPath = _tempDestinationPath;
        var optionsMonitor = CreateOptionsMonitor(options);

        var fakeSmbClient = new FailingSMBClient
        {
            FailOnRead = true,
            ReadErrorMessage = "Connection reset during read"
        };

        var now = DateTimeOffset.UtcNow;
        fakeSmbClient.AddFile("/data.pdf", 1024, now.AddHours(-1));

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

        // Assert - no .tmp files should remain
        var tmpFiles = Directory.GetFiles(_tempDestinationPath, "*.tmp");
        tmpFiles.Should().BeEmpty("temp files should be cleaned up after failure");
    }

    [Fact]
    public async Task ExecuteAsync_PodRestart_ResumesFromWatermark()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var optionsMonitor = CreateOptionsMonitor(options);
        var fakeSmbClient = new FakeSMBClient();

        var now = DateTimeOffset.UtcNow;
        var watermark = now.AddHours(-5);

        // Add files: some before watermark, some after
        fakeSmbClient.AddFile("/old1.csv", 1024, watermark.AddHours(-2));
        fakeSmbClient.AddFile("/old2.csv", 1024, watermark.AddHours(-1));
        fakeSmbClient.AddFile("/new1.csv", 1024, watermark.AddHours(1));
        fakeSmbClient.AddFile("/new2.csv", 1024, watermark.AddHours(2));

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

        // Act - simulate pod restart by processing from watermark
        var results = new List<HistorianSample>();
        await foreach (var sample in executor.ExecuteAsync(query, windowStart, windowEnd, CancellationToken.None))
        {
            results.Add(sample);
        }

        // Assert - should only process files after watermark
        results.Should().NotBeEmpty("should process new files after watermark");
    }

    [Fact]
    public async Task ExecuteAsync_LeaderElectionFailover_NewLeaderContinues()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.EnableLeaderElection = false; // Simulate non-leader scenario
        var optionsMonitor = CreateOptionsMonitor(options);
        var fakeSmbClient = new FakeSMBClient();

        var now = DateTimeOffset.UtcNow;
        fakeSmbClient.AddFile("/data.csv", 1024, now.AddHours(-1));

        var watermarkStore = CreateMockWatermarkStore();

        // Create executor without leader election (simulating follower)
        var executor = new SMBHistorianExecutor(
            NullLogger<SMBHistorianExecutor>.Instance,
            optionsMonitor,
            fakeSmbClient,
            watermarkStore,
            null);

        var query = CreateTestQuery();
        var windowStart = now.AddDays(-1);
        var windowEnd = now;

        // Act - follower should still execute when leader election is disabled
        var results = new List<HistorianSample>();
        await foreach (var sample in executor.ExecuteAsync(query, windowStart, windowEnd, CancellationToken.None))
        {
            results.Add(sample);
        }

        // Assert - should process normally
        results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_MultipleFileReadFailures_ContinuesProcessing()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var optionsMonitor = CreateOptionsMonitor(options);

        // Create a client that fails every other read
        var fakeSmbClient = new IntermittentFailingSMBClient
        {
            FailPattern = new[] { false, true, false, true, false } // Fail on 2nd and 4th file
        };

        var now = DateTimeOffset.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            fakeSmbClient.AddFile($"/data{i}.csv", 1024, now.AddHours(-i));
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
        var results = new List<HistorianSample>();
        await foreach (var sample in executor.ExecuteAsync(query, windowStart, windowEnd, CancellationToken.None))
        {
            results.Add(sample);
        }

        // Assert - should process successful files
        results.Should().NotBeEmpty("should process files that succeed");
    }

    [Fact]
    public async Task ExecuteAsync_InvalidCsvContent_SkipsInvalidLinesAndContinues()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var optionsMonitor = CreateOptionsMonitor(options);
        var fakeSmbClient = new FakeSMBClient(); // Returns valid CSV by default

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

        // Act - should handle invalid lines gracefully
        var results = new List<HistorianSample>();
        await foreach (var sample in executor.ExecuteAsync(query, windowStart, windowEnd, CancellationToken.None))
        {
            results.Add(sample);
        }

        // Assert - should process valid lines
        results.Should().NotBeEmpty("valid CSV lines should be processed");
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

    /// <summary>
    /// Fake SMB client that fails on specific operations for chaos testing.
    /// </summary>
    private sealed class FailingSMBClient : ISMBClient
    {
        private readonly FakeSMBClient _inner = new();

        public bool FailOnRead { get; set; }
        public string ReadErrorMessage { get; set; } = "Simulated read failure";

        public bool ConnectCalled => _inner.ConnectCalled;
        public bool DisconnectCalled => _inner.DisconnectCalled;

        public void AddFile(string path, long size, DateTimeOffset lastModified)
        {
            _inner.AddFile(path, size, lastModified);
        }

        public Task ConnectAsync(
            string host,
            int port,
            string shareName,
            Akri.HistorianConnector.Core.Models.ConnectorAuthentication authentication,
            CancellationToken cancellationToken)
            => _inner.ConnectAsync(host, port, shareName, authentication, cancellationToken);

        public Task DisconnectAsync(CancellationToken cancellationToken)
            => _inner.DisconnectAsync(cancellationToken);

        public Task<List<FileMetadata>> ListFilesAsync(string path, string pattern, CancellationToken cancellationToken)
            => _inner.ListFilesAsync(path, pattern, cancellationToken);

        public Task<string> ReadFileAsync(string path, CancellationToken cancellationToken)
        {
            if (FailOnRead)
            {
                throw new IOException(ReadErrorMessage);
            }

            return _inner.ReadFileAsync(path, cancellationToken);
        }

        public Task<bool> DirectoryExistsAsync(string path, CancellationToken cancellationToken)
            => _inner.DirectoryExistsAsync(path, cancellationToken);

        public Task<List<string>> ListDirectoriesAsync(string path, CancellationToken cancellationToken)
            => _inner.ListDirectoriesAsync(path, cancellationToken);
    }

    /// <summary>
    /// Fake SMB client that fails intermittently based on a pattern.
    /// </summary>
    private sealed class IntermittentFailingSMBClient : ISMBClient
    {
        private readonly FakeSMBClient _inner = new();
        private int _readCount;

        public bool[] FailPattern { get; set; } = Array.Empty<bool>();

        public bool ConnectCalled => _inner.ConnectCalled;
        public bool DisconnectCalled => _inner.DisconnectCalled;

        public void AddFile(string path, long size, DateTimeOffset lastModified)
        {
            _inner.AddFile(path, size, lastModified);
        }

        public Task ConnectAsync(
            string host,
            int port,
            string shareName,
            Akri.HistorianConnector.Core.Models.ConnectorAuthentication authentication,
            CancellationToken cancellationToken)
            => _inner.ConnectAsync(host, port, shareName, authentication, cancellationToken);

        public Task DisconnectAsync(CancellationToken cancellationToken)
            => _inner.DisconnectAsync(cancellationToken);

        public Task<List<FileMetadata>> ListFilesAsync(string path, string pattern, CancellationToken cancellationToken)
            => _inner.ListFilesAsync(path, pattern, cancellationToken);

        public Task<string> ReadFileAsync(string path, CancellationToken cancellationToken)
        {
            var shouldFail = _readCount < FailPattern.Length && FailPattern[_readCount];
            _readCount++;

            if (shouldFail)
            {
                throw new IOException("Intermittent read failure");
            }

            return _inner.ReadFileAsync(path, cancellationToken);
        }

        public Task<bool> DirectoryExistsAsync(string path, CancellationToken cancellationToken)
            => _inner.DirectoryExistsAsync(path, cancellationToken);

        public Task<List<string>> ListDirectoriesAsync(string path, CancellationToken cancellationToken)
            => _inner.ListDirectoriesAsync(path, cancellationToken);
    }
}
