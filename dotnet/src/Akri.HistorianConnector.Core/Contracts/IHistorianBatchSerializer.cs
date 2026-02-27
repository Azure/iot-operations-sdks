// <copyright file="IHistorianBatchSerializer.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

using Akri.HistorianConnector.Core.Models;

namespace Akri.HistorianConnector.Core.Contracts;

/// <summary>
/// Serializes historian batch data for MQTT publishing.
/// Implement this interface to customize the output format.
/// </summary>
public interface IHistorianBatchSerializer
{
    /// <summary>
    /// Gets the content type for the serialized data (e.g., "application/json").
    /// </summary>
    string ContentType { get; }

    /// <summary>
    /// Serializes a historian batch to bytes for MQTT publishing.
    /// </summary>
    /// <param name="batch">The batch to serialize.</param>
    /// <returns>The serialized bytes.</returns>
    byte[] Serialize(HistorianBatch batch);
}

/// <summary>
/// Default JSON serializer for historian batches.
/// </summary>
public sealed class JsonHistorianBatchSerializer : IHistorianBatchSerializer
{
    private static readonly System.Text.Json.JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <inheritdoc />
    public string ContentType => "application/json";

    /// <inheritdoc />
    public byte[] Serialize(HistorianBatch batch)
    {
        return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(batch, _options);
    }
}
