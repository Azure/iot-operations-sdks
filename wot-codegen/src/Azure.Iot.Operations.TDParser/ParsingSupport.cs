// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TDParser
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;

    public static class ParsingSupport
    {
        private static string[] Prefixes = new string[] { "aov:", "dtv:", "dov:" };

        public static void CheckForDuplicatePropertyName(ref Utf8JsonReader reader, string propertyName, Dictionary<string, long> propertyNames, string objectDesc)
        {
            if (propertyNames.ContainsKey(propertyName))
            {
                Skip(ref reader);
                throw new InvalidOperationException($"duplicate property name '{propertyName}' found in {objectDesc} object");
            }

            foreach (string variant in GetPropertyNameVariants(propertyName))
            {
                if (propertyNames.ContainsKey(variant))
                {
                    Skip(ref reader);
                    throw new InvalidOperationException($"two variants of same property name '{propertyName}' and '{variant}' found in {objectDesc} object");
                }
            }
        }

        private static void Skip(ref Utf8JsonReader reader)
        {
            while (reader.TokenType == JsonTokenType.PropertyName)
            {
                reader.Read();
                reader.Skip();
                reader.Read();
            }
        }

        private static IEnumerable<string> GetPropertyNameVariants(string propertyName)
        {
            string? givenPrefix = Prefixes.FirstOrDefault(p => propertyName.StartsWith(p));
            if (givenPrefix == null)
            {
                yield break;
            }

            foreach (string prefix in Prefixes)
            {
                if (prefix != givenPrefix)
                {
                    yield return prefix + propertyName.Substring(givenPrefix.Length);
                }
            }
        }
    }
}
