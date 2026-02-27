// <copyright file="DataPointConfiguration.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

using System.Text.Json.Serialization;

namespace Akri.Connector.SMB.Models;

public sealed class DataPointConfiguration
{
    [JsonPropertyName("timestampColumn")]
    public string? TimestampColumn { get; init; }

    [JsonPropertyName("tagColumn")]
    public string? TagColumn { get; init; }

    [JsonPropertyName("valueColumn")]
    public string? ValueColumn { get; init; }

    [JsonPropertyName("qualityColumn")]
    public string? QualityColumn { get; init; }

    [JsonPropertyName("delimiter")]
    public string? Delimiter { get; init; } = ",";

    [JsonPropertyName("timestampFormat")]
    public string? TimestampFormat { get; init; }
}
