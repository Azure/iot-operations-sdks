// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Azure.Iot.Operations.Protocol.Models
{
    /// <summary>
    /// A utility class for parsing + stringifying protocol versions and protocol
    /// version lists
    /// </summary>
    internal class ProtocolVersion
    {
        internal int MajorVersion { get; }

        internal int MinorVersion { get; }

        internal ProtocolVersion(int majorVersion, int minorVersion)
        {
            MajorVersion = majorVersion;
            MinorVersion = minorVersion;
        }

        internal static bool TryParseProtocolVersion(string? protocolVersionString, out ProtocolVersion? protocolVersion)
        {
            // If no protocol version is provided, assume version 1.0
            int requestMajorProtocolVersion = 1;
            int requestMinorProtocolVersion = 0;

            if (protocolVersionString != null)
            {
                string[] requestProtocolVersionsSplit = protocolVersionString.Split('.');
                if (requestProtocolVersionsSplit.Length != 2
                    || !int.TryParse(requestProtocolVersionsSplit[0], out requestMajorProtocolVersion)
                    || !int.TryParse(requestProtocolVersionsSplit[1], out requestMinorProtocolVersion))
                {
                    protocolVersion = null;
                    return false;
                }
            }

            protocolVersion = new(requestMajorProtocolVersion, requestMinorProtocolVersion);
            return true;
        }

        internal static bool TryParseFromString(string majorProtocolVersionArrayString, out int[] majorProtocolVersionArray)
        {
            List<int> supportedMajorProtocolVersions = [];
            foreach (string majorProtocolVersion in majorProtocolVersionArrayString.Split(" "))
            {
                if (int.TryParse(majorProtocolVersion, out int value))
                {
                    supportedMajorProtocolVersions.Add(value);
                }
                else
                {
                    majorProtocolVersionArray = [];
                    return false;
                }
            }

            majorProtocolVersionArray = [.. supportedMajorProtocolVersions];
            return true;
        }

        internal static string ToString(int[] majorProtocolVersionArray)
        {
            string spaceSeparatedListOfSupportedProtocolVersions = "";
            foreach (int supportedProtocolVersion in majorProtocolVersionArray)
            {
                spaceSeparatedListOfSupportedProtocolVersions += $" {supportedProtocolVersion}";
            }

            return spaceSeparatedListOfSupportedProtocolVersions.TrimStart(' ');
        }
    }
}
