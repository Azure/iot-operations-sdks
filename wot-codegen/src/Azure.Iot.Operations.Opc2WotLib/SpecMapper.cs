// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System;
    using System.Linq;

    public class SpecMapper
    {
        public static string GetSpecNameFromUri(string modelUri)
        {
            Uri uri = new Uri(modelUri);
            string result = GetPrefixFromHost(uri.Host) + GetNameFromPath(uri.AbsolutePath);

            if (result == "UA")
            {
                return "OpcUaCore";
            }

            return result.StartsWith("UA.", StringComparison.Ordinal) ? result.Substring("UA.".Length) : result;
        }

        public static string GetNameFromPath(string uriPath)
        {
            // Drop purely-numeric path segments (e.g., the "2013/01" in dated legacy
            // namespaces like http://www.OPCFoundation.org/UA/2013/01/ISA95) so that the
            // derived spec name and type titles do not start with a digit.
            return string.Join('.', uriPath
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Where(segment => !segment.All(char.IsDigit)));
        }

        public static string GetPrefixFromHost(string uriHost)
        {
            string[] parts = uriHost.Split('.');
            return string.Equals(parts[parts.Length - 2], "opcfoundation", StringComparison.OrdinalIgnoreCase)
                && string.Equals(parts[parts.Length - 1], "org", StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : parts[parts.Length - 2] + ".";
        }
    }
}
