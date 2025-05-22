// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Azure.Iot.Operations.Protocol.Chunking;

public static class Utils
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
}
