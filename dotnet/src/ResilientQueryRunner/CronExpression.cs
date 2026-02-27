// <copyright file="CronExpression.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

namespace ResilientQueryRunner;

/// <summary>
/// Minimal cron expression with seconds resolution: "sec min hour dom month dow".
/// Wraps the Cronos library for battle-tested cron evaluation with timezone and DST support.
/// Supports: '*', '*/n', 'a-b', 'a,b,c'.
/// </summary>
public sealed class CronExpression
{
    private readonly Cronos.CronExpression _cronosExpression;

    private CronExpression(Cronos.CronExpression cronosExpression)
    {
        _cronosExpression = cronosExpression;
    }

    /// <summary>
    /// Parses a cron expression with 6 fields: sec min hour dom month dow.
    /// </summary>
    public static CronExpression Parse(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new ArgumentException("Cron expression is required.", nameof(expression));
        }

        var parts = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 6)
        {
            throw new FormatException("Cron expression must have 6 fields: sec min hour dom month dow.");
        }

        // Cronos uses 5-field format (min hour dom month dow) by default
        // We need to use the format that includes seconds
        try
        {
            var cronosExpression = Cronos.CronExpression.Parse(expression, Cronos.CronFormat.IncludeSeconds);
            return new CronExpression(cronosExpression);
        }
        catch (Cronos.CronFormatException ex)
        {
            throw new FormatException($"Invalid cron expression: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Returns the last tick (in UTC) at or before <paramref name="nowUtc"/>.
    /// </summary>
    public DateTimeOffset GetLastTickUtc(DateTimeOffset nowUtc, TimeZoneInfo timeZone)
    {
        // Cronos requires UTC DateTime for GetOccurrences
        var nowUtcDateTime = DateTime.SpecifyKind(nowUtc.UtcDateTime, DateTimeKind.Utc);
        var fromUtcDateTime = DateTime.SpecifyKind(nowUtc.AddYears(-1).UtcDateTime, DateTimeKind.Utc);

        // Get the last occurrence at or before the current time
        // Cronos returns DateTime values in UTC when using GetOccurrences with a timezone
        var occurrence = _cronosExpression.GetOccurrences(
            fromUtcDateTime, // Start from 1 year ago for safety
            nowUtcDateTime,
            timeZone,
            fromInclusive: true,
            toInclusive: true)
            .LastOrDefault();

        if (occurrence == default)
        {
            throw new InvalidOperationException("Unable to find a matching cron tick within search window.");
        }

        // Cronos returns DateTime in UTC when a timezone is specified
        var utc = new DateTimeOffset(occurrence, TimeSpan.Zero);

        // Ensure we don't return a time in the future (should not happen, but safety check)
        if (utc > nowUtc)
        {
            // Try again with a shorter window
            fromUtcDateTime = DateTime.SpecifyKind(nowUtc.AddMonths(-1).UtcDateTime, DateTimeKind.Utc);
            var toUtcDateTime = DateTime.SpecifyKind(nowUtc.AddSeconds(-1).UtcDateTime, DateTimeKind.Utc);

            occurrence = _cronosExpression.GetOccurrences(
                fromUtcDateTime,
                toUtcDateTime,
                timeZone,
                fromInclusive: true,
                toInclusive: true)
                .LastOrDefault();

            if (occurrence == default)
            {
                throw new InvalidOperationException("Unable to find a matching cron tick within search window.");
            }

            utc = new DateTimeOffset(occurrence, TimeSpan.Zero);
        }

        return utc;
    }

    public override string ToString()
        => _cronosExpression.ToString();
}
