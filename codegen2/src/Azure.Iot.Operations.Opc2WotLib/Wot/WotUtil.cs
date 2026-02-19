// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System.Text.RegularExpressions;

    public static class WotUtil
    {
        public static string LegalizeName(string name, string prefix = "")
        {
            string legalPrefix = prefix == string.Empty ? string.Empty : Capitalize(Regex.Replace($"{prefix}_", "[^a-zA-Z0-9]+", "_", RegexOptions.CultureInvariant));
            string legalName = Capitalize(Regex.Replace(name, "[^a-zA-Z0-9]+", "_", RegexOptions.CultureInvariant));
            return $"{legalPrefix}{legalName}";
        }

        private static string Capitalize(string str)
        {
            return char.ToUpper(str[0]) + str.Substring(1);
        }
    }
}
