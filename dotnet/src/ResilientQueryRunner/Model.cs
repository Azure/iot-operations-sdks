// <copyright file="Model.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

namespace ResilientQueryRunner;

/// <summary>
/// Specifies the kind of watermark used for tracking query progress.
/// </summary>
public enum WatermarkKind
{
    Time,
    Index,
}

/// <summary>
/// Represents a unique identifier for a query.
/// </summary>
public readonly record struct QueryId(string Value)
{
    /// <summary>
    /// Returns the string representation of the query ID.
    /// </summary>
    public override string ToString() => Value;
}

/// <summary>
/// Represents a unique identifier for a query run.
/// </summary>
public readonly record struct RunId(Guid Value)
{
    /// <summary>
    /// Creates a new unique run ID.
    /// </summary>
    public static RunId New() => new(Guid.NewGuid());

    /// <summary>
    /// Returns the string representation of the run ID.
    /// </summary>
    public override string ToString() => Value.ToString();
}

/// <summary>
/// Represents a watermark indicating the progress of a query.
/// </summary>
public readonly record struct Watermark
{
    public WatermarkKind Kind { get; }
    public DateTimeOffset? TimeUtc { get; }
    public long? Index { get; }

    private Watermark(WatermarkKind kind, DateTimeOffset? timeUtc, long? index)
    {
        Kind = kind;
        TimeUtc = timeUtc;
        Index = index;
    }

    /// <summary>
    /// Creates a time-based watermark.
    /// </summary>
    public static Watermark ForTime(DateTimeOffset exclusiveEndUtc) => new(WatermarkKind.Time, exclusiveEndUtc, null);

    /// <summary>
    /// Creates an index-based watermark.
    /// </summary>
    public static Watermark ForIndex(long exclusiveEndIndex) => new(WatermarkKind.Index, null, exclusiveEndIndex);

    /// <summary>
    /// Returns the maximum of two watermarks.
    /// </summary>
    public static Watermark Max(Watermark left, Watermark right)
    {
        if (left.Kind != right.Kind)
        {
            throw new InvalidOperationException("Cannot compare watermarks of different kinds.");
        }

        return left.Kind switch
        {
            WatermarkKind.Time => left.TimeUtc!.Value >= right.TimeUtc!.Value ? left : right,
            WatermarkKind.Index => left.Index!.Value >= right.Index!.Value ? left : right,
            _ => throw new InvalidOperationException($"Unsupported kind {left.Kind}"),
        };
    }

    /// <summary>
    /// Returns the string representation of the watermark.
    /// </summary>
    public override string ToString() => Kind switch
    {
        WatermarkKind.Time => $"time:{TimeUtc:O}",
        WatermarkKind.Index => $"index:{Index}",
        _ => Kind.ToString(),
    };
}

/// <summary>
/// Represents a range of data to query.
/// </summary>
public readonly record struct QueryRange
{
    public WatermarkKind Kind { get; }
    public DateTimeOffset? FromTimeUtc { get; }
    public DateTimeOffset? ToTimeUtcExclusive { get; }
    public long? FromIndex { get; }
    public long? ToIndexInclusive { get; }

    private QueryRange(
        WatermarkKind kind,
        DateTimeOffset? fromTimeUtc,
        DateTimeOffset? toTimeUtcExclusive,
        long? fromIndex,
        long? toIndexInclusive)
    {
        Kind = kind;
        FromTimeUtc = fromTimeUtc;
        ToTimeUtcExclusive = toTimeUtcExclusive;
        FromIndex = fromIndex;
        ToIndexInclusive = toIndexInclusive;
    }

    /// <summary>
    /// Creates a time-based range.
    /// </summary>
    public static QueryRange Time(DateTimeOffset fromInclusiveUtc, DateTimeOffset toExclusiveUtc)
    {
        if (toExclusiveUtc < fromInclusiveUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(toExclusiveUtc), "to must be >= from");
        }

        return new QueryRange(WatermarkKind.Time, fromInclusiveUtc, toExclusiveUtc, null, null);
    }

    /// <summary>
    /// Creates an index-based range.
    /// </summary>
    public static QueryRange Index(long fromInclusive, long toInclusive)
    {
        if (toInclusive < fromInclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(toInclusive), "to must be >= from");
        }

        return new QueryRange(WatermarkKind.Index, null, null, fromInclusive, toInclusive);
    }

    /// <summary>
    /// Gets the candidate exclusive end watermark for this range.
    /// </summary>
    public Watermark CandidateExclusiveEndWatermark() => Kind switch
    {
        WatermarkKind.Time => Watermark.ForTime(ToTimeUtcExclusive!.Value),
        WatermarkKind.Index => Watermark.ForIndex(ToIndexInclusive!.Value + 1),
        _ => throw new InvalidOperationException($"Unsupported kind {Kind}"),
    };

    /// <summary>
    /// Returns the string representation of the range.
    /// </summary>
    public override string ToString() => Kind switch
    {
        WatermarkKind.Time => $"[{FromTimeUtc:O}, {ToTimeUtcExclusive:O})",
        WatermarkKind.Index => $"[{FromIndex}, {ToIndexInclusive}]",
        _ => Kind.ToString(),
    };
}

/// <summary>
/// Parameters for defining query ranges.
/// </summary>
public sealed record RangeParameters
{
    public TimeSpan? WindowDuration { get; init; }
    public int? WindowSize { get; init; }
    public TimeSpan AvailabilityDelay { get; init; } = TimeSpan.Zero;
    public TimeSpan Overlap { get; init; } = TimeSpan.Zero;
    public int CatchUpMaxWindowsPerTick { get; init; } = 100;

    /// <summary>
    /// Validates the parameters for the given watermark kind.
    /// </summary>
    public void ValidateFor(WatermarkKind kind)
    {
        if (CatchUpMaxWindowsPerTick <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(CatchUpMaxWindowsPerTick));
        }

        if (AvailabilityDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(AvailabilityDelay));
        }

        if (Overlap < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(Overlap));
        }

        switch (kind)
        {
            case WatermarkKind.Time:
                if (WindowDuration is null || WindowDuration.Value <= TimeSpan.Zero)
                {
                    throw new InvalidOperationException("WindowDuration must be set for time-based queries.");
                }

                break;

            case WatermarkKind.Index:
                if (WindowSize is null || WindowSize.Value <= 0)
                {
                    throw new InvalidOperationException("WindowSize must be set for index-based queries.");
                }

                break;
        }
    }
}

/// <summary>
/// Parameters for query priority.
/// </summary>
public sealed record PriorityParameters
{
    public int BasePriority { get; init; }
    public bool MustRunOnSchedule { get; init; }
    public TimeSpan? SlaTolerance { get; init; }
}

/// <summary>
/// Defines a query to be executed by the engine.
/// </summary>
public sealed record QueryDefinition
{
    public required QueryId QueryId { get; init; }
    public required CronExpression Cron { get; init; }
    public TimeZoneInfo TimeZone { get; init; } = TimeZoneInfo.Utc;
    public required WatermarkKind WatermarkKind { get; init; }
    public required RangeParameters RangeParameters { get; init; }
    public PriorityParameters PriorityParameters { get; init; } = new();
    public object? Payload { get; init; }
}
