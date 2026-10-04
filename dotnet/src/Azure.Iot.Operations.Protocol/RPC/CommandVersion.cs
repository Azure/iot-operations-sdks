// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.RPC
{
    internal static class CommandVersion
    {
        internal static readonly int[] SupportedMajorProtocolVersions = [MajorProtocolVersion];

        internal const int MajorProtocolVersion = 1;
        internal const int MinorProtocolVersion = 0;
        internal const int ChunkingMajorProtocolVersion = 2;
        internal const int ChunkingMinorProtocolVersion = 0;

        internal static readonly int[] SupportedResponseMajorProtocolVersions =
            [MajorProtocolVersion, ChunkingMajorProtocolVersion];
    }
}