// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Azure.Iot.Operations.Protocol.Chunking;

internal static class Utils
{
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
