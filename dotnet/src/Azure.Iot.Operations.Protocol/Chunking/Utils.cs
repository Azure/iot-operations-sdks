// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Azure.Iot.Operations.Protocol.Chunking;

internal static class Utils
{
    /// <summary>
    /// Calculates the maximum size for a message chunk based on max packet size and overhead.
    /// </summary>
    /// <param name="maxPacketSize">The maximum packet size allowed by the broker.</param>
    /// <param name="staticOverhead">The static overhead to account for in each chunk.</param>
    /// <returns>The maximum size that can be used for a message chunk.</returns>
    public static int GetMaxChunkSize(int maxPacketSize, int staticOverhead)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxPacketSize, staticOverhead);
        return maxPacketSize - staticOverhead;
    }

    /// <summary>
    /// Whole seconds remaining until <paramref name="deadline"/>, rounded up, or zero once it has
    /// passed.
    /// </summary>
    /// <remarks>
    /// Zero is the MQTT "already expired" signal here, matching how the RPC envoys treat a
    /// <c>MessageExpiryInterval</c> of zero, so callers must not publish a message carrying it.
    /// </remarks>
    public static uint RemainingExpirySeconds(DateTime deadline, DateTime now)
    {
        double seconds = Math.Ceiling((deadline - now).TotalSeconds);

        return seconds <= 0 ? 0 : (uint)Math.Min(seconds, uint.MaxValue);
    }
}
