// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System;

    public class SpecMapper
    {
        public static string GetSpecNameFromUri(string modelUri)
        {
            Uri uri = new Uri(modelUri);
            string result = GetPrefixFromHost(uri.Host) + GetNameFromPath(uri.AbsolutePath);
            return result == "UA" ? "OpcUaCore" : result;
        }

        public static string GetNameFromPath(string uriPath)
        {
            return uriPath.Trim('/').Replace('/', '.');
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
