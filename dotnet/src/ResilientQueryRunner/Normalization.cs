// <copyright file="Normalization.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

namespace ResilientQueryRunner;

/// <summary>
/// Represents a normalized data record.
/// </summary>
public sealed record NormalizedRecord
{
    public required string SeriesId { get; init; }
    public required DateTimeOffset TimestampUtc { get; init; }
    public required double Value { get; init; }
    public int? Quality { get; init; }
}

/// <summary>
/// A batch of time series data.
/// </summary>
public sealed record TimeSeriesBatch
{
    public required string ConnectorIdentity { get; init; }
    public required QueryId QueryId { get; init; }
    public required RunId RunId { get; init; }
    public required QueryRange ExecutedRange { get; init; }
    public required Watermark CandidateWatermarkExclusiveEnd { get; init; }

    public required IReadOnlyList<TimeSeries> Series { get; init; }

    /// <summary>
    /// Represents a single time series within a batch.
    /// </summary>
    public sealed record TimeSeries
    {
        public required string Id { get; init; }
        public required long BaseEpochNs { get; init; }
        public required IReadOnlyList<long> OffsetNs { get; init; }
        public required IReadOnlyList<double> Values { get; init; }
        public IReadOnlyList<int>? Quality { get; init; }
    }
}

/// <summary>
/// Builds a normalized resultset from records.
/// </summary>
public sealed class NormalizedResultsetBuilder
{
    private readonly string _connectorIdentity;
    private readonly QueryId _queryId;
    private readonly RunId _runId;
    private readonly QueryRange _executedRange;
    private readonly Watermark _candidateWatermark;

    private readonly Dictionary<string, SeriesBuilder> _series = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the builder.
    /// </summary>
    public NormalizedResultsetBuilder(
        string connectorIdentity,
        QueryId queryId,
        RunId runId,
        QueryRange executedRange,
        Watermark candidateWatermarkExclusiveEnd)
    {
        _connectorIdentity = connectorIdentity;
        _queryId = queryId;
        _runId = runId;
        _executedRange = executedRange;
        _candidateWatermark = candidateWatermarkExclusiveEnd;
    }

    /// <summary>
    /// Adds a normalized record to the resultset.
    /// </summary>
    public void Add(NormalizedRecord record)
    {
        if (!_series.TryGetValue(record.SeriesId, out var sb))
        {
            sb = new SeriesBuilder(record.SeriesId);
            _series.Add(record.SeriesId, sb);
        }

        sb.Add(record);
    }

    /// <summary>
    /// Builds the time series batch.
    /// </summary>
    public TimeSeriesBatch Build()
    {
        var series = _series.Values
            .OrderBy(s => s.Id, StringComparer.Ordinal)
            .Select(s => s.Build())
            .ToArray();

        return new TimeSeriesBatch
        {
            ConnectorIdentity = _connectorIdentity,
            QueryId = _queryId,
            RunId = _runId,
            ExecutedRange = _executedRange,
            CandidateWatermarkExclusiveEnd = _candidateWatermark,
            Series = series,
        };
    }

    private sealed class SeriesBuilder
    {
        public string Id { get; }
        private long? _baseEpochNs;
        private readonly List<long> _offsetNs = new();
        private readonly List<double> _values = new();
        private List<int>? _quality;

        public SeriesBuilder(string id)
        {
            Id = id;
        }

        public void Add(NormalizedRecord record)
        {
            var epochNs = record.TimestampUtc.ToUnixTimeMilliseconds() * 1_000_000L;
            _baseEpochNs ??= epochNs;

            _offsetNs.Add(epochNs - _baseEpochNs.Value);
            _values.Add(record.Value);

            if (record.Quality is not null)
            {
                _quality ??= new List<int>(_values.Capacity);
                _quality.Add(record.Quality.Value);
            }
            else
            {
                _quality?.Add(0);
            }
        }

        public TimeSeriesBatch.TimeSeries Build() => new()
        {
            Id = Id,
            BaseEpochNs = _baseEpochNs ?? 0,
            OffsetNs = _offsetNs,
            Values = _values,
            Quality = _quality,
        };
    }
}
