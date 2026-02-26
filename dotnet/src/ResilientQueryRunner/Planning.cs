// <copyright file="Planning.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

namespace ResilientQueryRunner;

/// <summary>
/// Represents a planned run for a query.
/// </summary>
public sealed record RunPlan
{
    public required RunId RunId { get; init; }
    public required QueryId QueryId { get; init; }
    public required QueryRange Range { get; init; }
    public required Watermark CandidateWatermarkExclusiveEnd { get; init; }
    public int EffectivePriority { get; init; }
    public bool IsAdhoc { get; init; }
    public bool AdvanceWatermark { get; init; } = true;
    public int Attempt { get; init; } = 1;
}

/// <summary>
/// Plans ranges for query execution.
/// </summary>
public static class RangePlanner
{
    /// <summary>
    /// Plans ranges based on the watermark and parameters.
    /// </summary>
    public static IReadOnlyList<QueryRange> PlanRanges(
        WatermarkKind kind,
        Watermark watermark,
        RangeParameters rangeParameters,
        DateTimeOffset targetToUtc)
    {
        rangeParameters.ValidateFor(kind);

        var maxWindows = rangeParameters.CatchUpMaxWindowsPerTick;

        return kind switch
        {
            WatermarkKind.Time => PlanTimeRanges(watermark, rangeParameters, targetToUtc, maxWindows),
            WatermarkKind.Index => PlanIndexRanges(watermark, rangeParameters, maxWindows),
            _ => throw new InvalidOperationException($"Unsupported kind {kind}"),
        };
    }

    private static IReadOnlyList<QueryRange> PlanTimeRanges(
        Watermark watermark,
        RangeParameters rangeParameters,
        DateTimeOffset targetToUtc,
        int maxWindows)
    {
        if (watermark.Kind != WatermarkKind.Time)
        {
            throw new ArgumentException("Expected time watermark.", nameof(watermark));
        }

        var window = rangeParameters.WindowDuration!.Value;
        var overlap = rangeParameters.Overlap;

        // Watermark represents the exclusive end of what we've already processed.
        // The next range starts exactly at the watermark.
        var from = watermark.TimeUtc!.Value;

        if (from > targetToUtc)
        {
            return Array.Empty<QueryRange>();
        }

        var ranges = new List<QueryRange>(capacity: Math.Min(maxWindows, 16));
        var cursor = from;

        for (var i = 0; i < maxWindows && cursor < targetToUtc; i++)
        {
            var to = cursor + window;
            if (to > targetToUtc)
            {
                to = targetToUtc;
            }

            // When querying, apply overlap to look back
            var queryFrom = cursor;
            if (overlap > TimeSpan.Zero && cursor > DateTimeOffset.MinValue.Add(overlap))
            {
                queryFrom = cursor - overlap;
            }

            ranges.Add(QueryRange.Time(queryFrom, to));

            // Advance cursor to the end of this window (without overlap)
            // The watermark will be updated to 'to', so the next range starts at 'to'
            cursor = to;
        }

        return ranges;
    }

    private static IReadOnlyList<QueryRange> PlanIndexRanges(
        Watermark watermark,
        RangeParameters rangeParameters,
        int maxWindows)
    {
        if (watermark.Kind != WatermarkKind.Index)
        {
            throw new ArgumentException("Expected index watermark.", nameof(watermark));
        }

        var windowSize = rangeParameters.WindowSize!.Value;

        // Watermark represents the exclusive end of what we've already processed.
        // The next range starts exactly at the watermark.
        var fromIndex = watermark.Index!.Value;

        // For index-based queries, we plan a single window per tick.
        // The catch-up logic (FR-008b) will determine if additional runs should be planned
        // based on whether the previous run returned a full page.
        // Initially, plan only the first window; subsequent windows are planned dynamically
        // by the engine when a full page is returned.
        var ranges = new List<QueryRange>(capacity: Math.Min(maxWindows, 1));

        // Plan one window at a time
        var toIndexInclusive = fromIndex + windowSize - 1;
        ranges.Add(QueryRange.Index(fromIndex, toIndexInclusive));

        return ranges;
    }
}
