// <copyright file="AssetToQueryMapperTests.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

using Akri.HistorianConnector.Core.Contracts;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Akri.Connector.SMB.Tests.Unit;

/// <summary>
/// Unit tests for the DefaultAssetToQueryMapper to ensure per-query schedule configuration.
/// </summary>
public sealed class AssetToQueryMapperTests
{
    private readonly HistorianAssetToQueryMapper _mapper = new(NullLogger<HistorianAssetToQueryMapper>.Instance);

    [Fact]
    public void MapDatasetToQuery_AssetLevelCronExpression_UsesAssetDefault()
    {
        // Arrange
        var asset = CreateAssetWithAttributes(new Dictionary<string, string>
        {
            ["cronExpression"] = "0 * * * *", // Every hour
            ["windowDurationSeconds"] = "3600"
        });
        var dataset = CreateDataset("test-dataset", dataPoints: CreateTestDataPoints());

        // Act
        var result = _mapper.MapDatasetToQueries("device1", CreateDevice(), "endpoint1", "asset1", asset, dataset);

        // Assert
        result.Should().HaveCount(1);
        result[0].CronExpression.Should().Be("0 * * * *");
        result[0].WindowDuration.Should().Be(TimeSpan.FromSeconds(3600));
    }

    [Fact]
    public void MapDatasetToQuery_DatasetLevelCronExpression_OverridesAssetDefault()
    {
        // Arrange
        var asset = CreateAssetWithAttributes(new Dictionary<string, string>
        {
            ["cronExpression"] = "0 * * * *", // Every hour (asset default)
            ["windowDurationSeconds"] = "3600"
        });
        var datasetConfig = /*lang=json,strict*/ @"{""cronExpression"": ""*/5 * * * *"",""windowDurationSeconds"": 300}";
        var dataset = CreateDataset("test-dataset", datasetConfiguration: datasetConfig, dataPoints: CreateTestDataPoints());

        // Act
        var result = _mapper.MapDatasetToQueries("device1", CreateDevice(), "endpoint1", "asset1", asset, dataset);

        // Assert
        result.Should().HaveCount(1);
        result[0].CronExpression.Should().Be("*/5 * * * *"); // Dataset override
        result[0].WindowDuration.Should().Be(TimeSpan.FromSeconds(300)); // Dataset override
    }

    [Fact]
    public void MapDatasetToQuery_MultipleDatasets_DifferentSchedules()
    {
        // Arrange
        var asset = CreateAssetWithAttributes(new Dictionary<string, string>
        {
            ["cronExpression"] = "0 * * * *", // Asset default: every hour
            ["windowDurationSeconds"] = "3600",
            ["availabilityDelaySeconds"] = "60"
        });

        var dataset1 = CreateDataset("parse-sensor-data",
            datasetConfiguration: /*lang=json,strict*/ @"{""cronExpression"": ""*/1 * * * *"",""windowDurationSeconds"": 60}",
            dataPoints: CreateTestDataPoints("sensor1"));
        var dataset2 = CreateDataset("copy-reports",
            datasetConfiguration: /*lang=json,strict*/ @"{""cronExpression"": ""0 * * * *"",""windowDurationSeconds"":  3600, ""availabilityDelaySeconds"": 300}",
            dataPoints: CreateTestDataPoints("report1"));

        // Act
        var result1 = _mapper.MapDatasetToQueries("device1", CreateDevice(), "endpoint1", "asset1", asset, dataset1);
        var result2 = _mapper.MapDatasetToQueries("device1", CreateDevice(), "endpoint1", "asset1", asset, dataset2);

        // Assert
        result1.Should().HaveCount(1);
        result1[0].CronExpression.Should().Be("*/1 * * * *"); // Every minute
        result1[0].WindowDuration.Should().Be(TimeSpan.FromSeconds(60));
        result1[0].AvailabilityDelay.Should().Be(TimeSpan.FromSeconds(60)); // Asset default

        result2.Should().HaveCount(1);
        result2[0].CronExpression.Should().Be("0 * * * *"); // Every hour (dataset override matches asset)
        result2[0].WindowDuration.Should().Be(TimeSpan.FromSeconds(3600));
        result2[0].AvailabilityDelay.Should().Be(TimeSpan.FromSeconds(300)); // Dataset override
    }

    [Fact]
    public void MapDatasetToQuery_InvalidDatasetConfigurationJson_FallsBackToAsset()
    {
        // Arrange
        var asset = CreateAssetWithAttributes(new Dictionary<string, string>
        {
            ["cronExpression"] = "0 * * * *",
            ["windowDurationSeconds"] = "3600"
        });
        var dataset = CreateDataset("test-dataset",
            datasetConfiguration: "invalid json {",
            dataPoints: CreateTestDataPoints());

        // Act
        var result = _mapper.MapDatasetToQueries("device1", CreateDevice(), "endpoint1", "asset1", asset, dataset);

        // Assert
        result.Should().HaveCount(1);
        result[0].CronExpression.Should().Be("0 * * * *"); // Falls back to asset
        result[0].WindowDuration.Should().Be(TimeSpan.FromSeconds(3600));
    }

    [Fact]
    public void MapDatasetToQuery_EmptyDatasetConfiguration_FallsBackToAsset()
    {
        // Arrange
        var asset = CreateAssetWithAttributes(new Dictionary<string, string>
        {
            ["cronExpression"] = "0 * * * *",
            ["windowDurationSeconds"] = "3600"
        });
        var dataset = CreateDataset("test-dataset",
            datasetConfiguration: "",
            dataPoints: CreateTestDataPoints());

        // Act
        var result = _mapper.MapDatasetToQueries("device1", CreateDevice(), "endpoint1", "asset1", asset, dataset);

        // Assert
        result.Should().HaveCount(1);
        result[0].CronExpression.Should().Be("0 * * * *");
        result[0].WindowDuration.Should().Be(TimeSpan.FromSeconds(3600));
    }

    [Fact]
    public void MapDatasetToQuery_NoDataPoints_MapsToScanAllQuery()
    {
        // Arrange – dataset created in AIO without any data points (dataPoints.limits.minimum: 0)
        var asset = CreateAssetWithAttributes(new Dictionary<string, string>
        {
            ["cronExpression"] = "*/5 * * * *",
            ["windowDurationSeconds"] = "300"
        });
        var dataset = CreateDataset("scan-all-dataset"); // no data points

        // Act
        var result = _mapper.MapDatasetToQueries("device1", CreateDevice(), "endpoint1", "asset1", asset, dataset);

        // Assert – mapper must produce a valid query; empty DataPoints = scan-all
        result.Should().HaveCount(1);
        result[0].DataPoints.Should().BeEmpty();
        result[0].CronExpression.Should().Be("*/5 * * * *");
        result[0].WindowDuration.Should().Be(TimeSpan.FromSeconds(300));
    }

    [Fact]
    public void MapDatasetToQuery_DatasetConfigurationWithNonHistorianKeys_IgnoresThem()
    {
        // Arrange
        var asset = CreateAssetWithAttributes(new Dictionary<string, string>
        {
            ["cronExpression"] = "0 * * * *"
        });
        var datasetConfig = /*lang=json,strict*/ @"{""custom.key"": ""value"", ""cronExpression"": ""*/10 * * * *""}";
        var dataset = CreateDataset("test-dataset", datasetConfiguration: datasetConfig, dataPoints: CreateTestDataPoints());

        // Act
        var result = _mapper.MapDatasetToQueries("device1", CreateDevice(), "endpoint1", "asset1", asset, dataset);

        // Assert
        result.Should().HaveCount(1);
        result[0].CronExpression.Should().Be("*/10 * * * *"); // Dataset cronExpression overrides asset; custom.key is ignored
    }

    [Fact]
    public void MapDatasetToQuery_ArrayConfiguration_MapsMultipleQueries()
    {
        // Arrange
        var asset = CreateAssetWithAttributes(new Dictionary<string, string>
        {
            ["cronExpression"] = "0 * * * *",
            ["windowDurationSeconds"] = "3600"
        });
        var datasetConfig = /*lang=json,strict*/ @"[
          {""QueryId"": ""q1"", ""TaskType"": ""Parse"", ""DirectoryPath"": ""/data"", ""FileFilter"": ""*.csv"", ""Schedule"": ""*/5 * * * *""},
          {""QueryId"": ""q2"", ""TaskType"": ""Copy"", ""DirectoryPath"": ""/docs"", ""FileFilter"": ""*.pdf"", ""Schedule"": ""0 * * * *""}
        ]";
        var dataset = CreateDataset("test-dataset", datasetConfiguration: datasetConfig, dataPoints: CreateTestDataPoints());

        // Act
        var result = _mapper.MapDatasetToQueries("device1", CreateDevice(), "endpoint1", "asset1", asset, dataset);

        // Assert
        result.Should().HaveCount(2);
        result[0].TaskType.Should().Be("Parse");
        result[0].BasePath.Should().Be("/data");
        result[0].FilePattern.Should().Be("*.csv");
        result[0].CronExpression.Should().Be("*/5 * * * *");

        result[1].TaskType.Should().Be("Copy");
        result[1].BasePath.Should().Be("/docs");
        result[1].FilePattern.Should().Be("*.pdf");
        result[1].CronExpression.Should().Be("0 * * * *");
    }

    private static Asset CreateAssetWithAttributes(Dictionary<string, string> attributes)
    {
        return new Asset { Attributes = attributes };
    }

    private static Device CreateDevice()
    {
        return new Device();
    }

    private static AssetDataset CreateDataset(string name, string? datasetConfiguration = null, List<AssetDatasetDataPoint>? dataPoints = null)
    {
        return new AssetDataset
        {
            Name = name,
            DatasetConfiguration = datasetConfiguration,
            DataPoints = dataPoints ?? new List<AssetDatasetDataPoint>()
        };
    }

    private static List<AssetDatasetDataPoint> CreateTestDataPoints(string dataSource = "tag1")
    {
        return new List<AssetDatasetDataPoint>
        {
            new()
            {
                DataSource = dataSource,
                Name = dataSource
            }
        };
    }
}
