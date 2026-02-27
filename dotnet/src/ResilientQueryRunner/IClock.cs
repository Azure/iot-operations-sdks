// <copyright file="IClock.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

namespace ResilientQueryRunner;

/// <summary>
/// Provides the current UTC time.
/// </summary>
public interface IClock
{
    /// <summary>
    /// Gets the current UTC time.
    /// </summary>
    DateTimeOffset UtcNow { get; }
}

/// <summary>
/// A system clock implementation.
/// </summary>
public sealed class SystemClock : IClock
{
    /// <summary>
    /// Gets the singleton instance of the system clock.
    /// </summary>
    public static readonly SystemClock Instance = new();

    private SystemClock() { }

    /// <summary>
    /// Gets the current UTC time.
    /// </summary>
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
