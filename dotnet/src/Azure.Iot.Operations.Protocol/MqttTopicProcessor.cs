// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Azure.Iot.Operations.Protocol
{
    /// <summary>
    /// Static class holding methods for processing MQTT topics, filters, and patterns.
    /// </summary>
    public static partial class MqttTopicProcessor
    {
        private static readonly Regex replaceableTokenRegex = new("{([^}]+)}");

        /// <summary>
        /// Determine whether a string is valid for use as a replacement string in a custom replacement map or a topic namespace.
        /// </summary>
        /// <param name="replacement">The string to validate.</param>
        /// <returns>True if and only if the replacement string is valid.</returns>
        internal static bool IsValidReplacement(string? replacement)
        {
            return !string.IsNullOrEmpty(replacement) && !ContainsInvalidChar(replacement) && !replacement.StartsWith('/') && !replacement.EndsWith('/') && !replacement.Contains("//");
        }

        /// <summary>
        /// Get an MQTT topic string for publication.
        /// </summary>
        /// <param name="pattern">The pattern whose tokens to replace.</param>
        /// <param name="tokenMap1">A first replacement map for replacing tokens in the provided pattern.</param>
        /// <returns>The MQTT topic for publication.</returns>
        /// <exception cref="ArgumentException">Thrown if the topic pattern is null or empty.</exception>
        public static string ResolveTopic(string pattern, IReadOnlyDictionary<string, string>? tokenMap1 = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(pattern, nameof(pattern));

            return replaceableTokenRegex.Replace(pattern.MapReplace(tokenMap1), "+");
        }

        public static bool DoesTopicMatchFilter(string topic, string filter)
        {
            return MqttTopicFilterComparer.Compare(topic, filter) == MqttTopicFilterCompareResult.IsMatch;
        }

        public static Dictionary<string, string> GetReplacementMap(string pattern, string topic)
        {
            Dictionary<string, string> replacementMap = new();

            string[] patternParts = pattern.Split('/');
            string[] topicParts = topic.Split('/');

            for (int i = 0; i < patternParts.Length; i++)
            {
                string patternPart = patternParts[i];
                if (patternPart.StartsWith('{') && patternPart.EndsWith('}'))
                {
                    replacementMap[patternPart.Substring(1, patternPart.Length - 2)] = topicParts[i];
                }
            }

            return replacementMap;
        }

        /// <summary>
        /// Validates that a topic pattern is valid for use in a Command or Telemetry.
        /// </summary>
        /// <param name="pattern">The topic pattern.</param>
        /// <param name="tokenMap">An optional token replacement map.</param>
        /// <param name="requireReplacement">True if a replacement value is required for any token in the pattern.</param>
        /// <param name="errMsg">Out parameter to receive error message if validation fails.</param>
        /// <param name="errToken">Out parameter to receive the value of a missing or invalid token, if any.</param>
        /// <param name="errReplacement">Out parameter to receive the value of a missing or invalid replacement, if any.</param>
        /// <returns>A <see cref="PatternValidity"/> value indicating whether the pattern is valid or the way in which it is invalid.</returns>
        public static PatternValidity ValidateTopicPattern(
            string? pattern,
            IReadOnlyDictionary<string, string>? tokenMap,
            bool requireReplacement,
            out string errMsg,
            out string? errToken,
            out string? errReplacement)
        {
            errToken = null;
            errReplacement = null;

            if (string.IsNullOrEmpty(pattern))
            {
                errMsg = "MQTT topic pattern must not be empty";
                return PatternValidity.InvalidPattern;
            }

            if (pattern.StartsWith('$'))
            {
                errMsg = "MQTT topic pattern starts with reserved character '$'";
                return PatternValidity.InvalidPattern;
            }

            foreach (string level in pattern.Split('/'))
            {
                if (level.Length == 0)
                {
                    errMsg = "MQTT topic pattern contains empty level";
                    return PatternValidity.InvalidPattern;
                }

                bool isToken = level.StartsWith('{') && level.EndsWith('}');
                if (isToken)
                {
                    string token = level[1..^1];
                    if (token.Length == 0)
                    {
                        errMsg = "Token in MQTT topic pattern is empty";
                        return PatternValidity.InvalidPattern;
                    }
                    else if (ContainsInvalidChar(token))
                    {
                        errMsg = $"Token '{level}' in MQTT topic pattern contains invalid character";
                        return PatternValidity.InvalidPattern;
                    }
                    else if (tokenMap == null)
                    {
                        if (requireReplacement)
                        {
                            errMsg = $"Token '{level}' in MQTT topic pattern, but no replacement map value provided";
                            errToken = token;
                            return PatternValidity.MissingReplacement;
                        }
                    }
                    else
                    {
                        string? replacement = null;
                        bool hasReplacement = tokenMap != null && tokenMap.TryGetValue(token, out replacement);
                        if (!hasReplacement)
                        {
                            if (requireReplacement)
                            {
                                errMsg = $"Token '{level}' in MQTT topic pattern, but key '{token}' not found in replacement map";
                                errToken = token;
                                return PatternValidity.MissingReplacement;
                            }
                        }
                        else if (hasReplacement && !IsValidReplacement(replacement))
                        {
                            errMsg = $"Token '{level}' in MQTT topic pattern has resident replacement value '{replacement}' that is not valid";
                            errToken = token;
                            errReplacement = replacement;
                            return PatternValidity.InvalidResidentReplacement;
                        }
                    }
                }
                else
                {
                    if (ContainsInvalidChar(level))
                    {
                        errMsg = $"Level '{level}' in MQTT topic pattern contains invalid character";
                        return PatternValidity.InvalidPattern;
                    }
                }
            }

            errMsg = string.Empty;
            return PatternValidity.Valid;
        }

        private static bool ContainsInvalidChar(string s)
        {
            foreach (char c in s)
            {
                if (c is < '!' or > '~' or '+' or '#' or '{' or '}')
                {
                    return true;
                }
            }

            return false;
        }

        private static string MapReplace(this string pattern, IReadOnlyDictionary<string, string>? tokenMap)
        {
            return tokenMap == null || !pattern.Contains('{')
                ? pattern
                : replaceableTokenRegex.Replace(pattern, (Match match) => tokenMap.TryGetValue(match.Groups[1].Captures[0].Value, out string? value) ? value : match.Groups[0].Captures[0].Value);
        }
    }
}
