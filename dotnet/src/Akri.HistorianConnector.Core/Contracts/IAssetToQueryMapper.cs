// <copyright file="IAssetToQueryMapper.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

using Akri.HistorianConnector.Core.Models;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Akri.HistorianConnector.Core.Contracts;

/// <summary>
/// Maps ADR Asset/Dataset definitions to HistorianQueryDefinitions.
/// Implement this interface to customize how ADR configuration is translated
/// to historian-specific query parameters.
/// </summary>
public interface IAssetToQueryMapper
{
    /// <summary>
    /// Maps an ADR Asset dataset to one or more HistorianQueryDefinitions.
    /// </summary>
    /// <param name="deviceName">The device name from ADR.</param>
    /// <param name="device">The device definition.</param>
    /// <param name="inboundEndpointName">The inbound endpoint name.</param>
    /// <param name="assetName">The asset name from ADR.</param>
    /// <param name="asset">The asset definition.</param>
    /// <param name="dataset">The dataset to map.</param>
    /// <returns>The historian query definitions, or an empty list if the dataset should be skipped.</returns>
    IReadOnlyList<HistorianQueryDefinition> MapDatasetToQueries(
        string deviceName,
        Device device,
        string inboundEndpointName,
        string assetName,
        Asset asset,
        AssetDataset dataset);
}
