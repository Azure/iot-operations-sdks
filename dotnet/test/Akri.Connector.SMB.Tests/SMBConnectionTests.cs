// <copyright file="SMBConnectionTests.cs">
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
/// Tests for SMB connection validation and configuration.
/// </summary>
public sealed class SMBConnectionTests
{
    [Fact]
    public async Task ListDirectoriesAsync_WhenConnected_ReturnsDirectoriesUnderPath()
    {
        // Arrange
        var fakeSmbClient = new FakeSMBClient();
        fakeSmbClient.AddDirectory("/data/archive");
        fakeSmbClient.AddDirectory("/logs");

        await fakeSmbClient.ConnectAsync(
            "test-host",
            445,
            "test-share",
            ConnectorAuthentication.Anonymous,
            CancellationToken.None);

        // Act
        var result = await fakeSmbClient.ListDirectoriesAsync("/data", CancellationToken.None);

        // Assert
        result.Should().Contain("/data/archive");
        result.Should().NotContain("/logs");
    }

    [Fact]
    public async Task ValidateAsync_WithValidConnection_ReturnsSuccess()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var optionsMonitor = CreateOptionsMonitor(options);
        var fakeSmbClient = new FakeSMBClient();
        fakeSmbClient.AddDirectory("/");
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
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        fakeSmbClient.ConnectCalled.Should().BeTrue();
        fakeSmbClient.DisconnectCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithNonExistentBasePath_ReturnsFailure()
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

        var query = CreateTestQuery() with { BasePath = "/nonexistent" };

        // Act
        var result = await executor.ValidateAsync(query, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Contain("does not exist");
    }

    [Fact]
    public async Task ValidateAsync_WhenConnectionFails_ReturnsFailure()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var optionsMonitor = CreateOptionsMonitor(options);
        
        // Create a mock SMB client that throws on connect
        var mockSmbClient = new Mock<ISMBClient>();
        mockSmbClient
            .Setup(c => c.ConnectAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<ConnectorAuthentication>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Connection refused"));
        
        var watermarkStore = CreateMockWatermarkStore();
        
        var executor = new SMBHistorianExecutor(
            NullLogger<SMBHistorianExecutor>.Instance,
            optionsMonitor,
            mockSmbClient.Object,
            watermarkStore,
            null);

        var query = CreateTestQuery();

        // Act
        var result = await executor.ValidateAsync(query, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Contain("Connection refused");
    }

    [Fact]
    public async Task ValidateAsync_WithCancellation_ThrowsOperationCanceledException()
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
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => executor.ValidateAsync(query, cts.Token));
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
                }
            }
        };
    }
}
