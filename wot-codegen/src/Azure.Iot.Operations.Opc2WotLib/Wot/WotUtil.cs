// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System;
    using System.Text;
    using System.Text.RegularExpressions;

    public static class WotUtil
    {
        public static string LegalizeName(string name, string prefix = "")
        {
            string legalPrefix = prefix == string.Empty ? string.Empty : Capitalize(Regex.Replace($"{prefix}_", "[^a-zA-Z0-9]+", "_", RegexOptions.CultureInvariant));
            string legalName = Capitalize(Regex.Replace(name, "[^a-zA-Z0-9]+", "_", RegexOptions.CultureInvariant));
            return $"{legalPrefix}{legalName}";
        }

        public static string GetTypeRef(string namespaceUri, string typeName)
        {
            _ = new Uri(namespaceUri, UriKind.Absolute);
            return $"{EncodeNamespaceUri(namespaceUri)}.{Uri.EscapeDataString(typeName)}";
        }

        public static string GetThingModelId(string typeRef)
        {
            return $"urn:{typeRef}";
        }

        private static string Capitalize(string str)
        {
            return char.ToUpper(str[0]) + str.Substring(1);
        }

        private static string EncodeNamespaceUri(string namespaceUri)
        {
            // Base64url preserves the complete URI and cannot contain the dot that separates the type name.
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(namespaceUri))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }
}
