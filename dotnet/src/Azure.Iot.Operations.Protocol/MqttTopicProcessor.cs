namespace Azure.Iot.Operations.Protocol
{
    using Azure.Iot.Operations.Protocol.Models;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Static class holding methods for processing MQTT topics, filters, and patterns.
    /// </summary>
    internal static class MqttTopicProcessor
    {
        private static readonly Regex replaceableTokenRegex = new Regex("{([^}]+)}");

        /// <summary>
        /// Determine whether a string is valid for use as a replacement string in a custom replacement map or a topic namespace.
        /// </summary>
        /// <param name="replacement">The string to validate.</param>
        /// <returns>True if and only if the replacement string is valid.</returns>
        internal static bool IsValidReplacement(string? replacement)
        {
            if (string.IsNullOrEmpty(replacement))
            {
                return false;
            }

            if (ContainsInvalidChar(replacement))
            {
                return false;
            }

            if (replacement.StartsWith('/') || replacement.EndsWith('/') || replacement.Contains("//"))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Get an MQTT topic string for publication.
        /// </summary>
        /// <param name="pattern">The pattern whose tokens to replace.</param>
        /// <param name="tokenMap1">A first replacement map for replacing tokens in the provided pattern.</param>
        /// <param name="tokenMap1">A second replacement map for replacing tokens in the provided pattern.</param>
        /// <returns><returns>The MQTT topic for publication.</returns>
        /// <exception cref="ArgumentException">Thrown if the topic pattern is null or empty.</exception></exception>
        public static string ResolveTopic(string pattern, IReadOnlyDictionary<string, string>? tokenMap1 = null, IReadOnlyDictionary<string, string>? tokenMap2 = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(pattern, nameof(pattern));

            return replaceableTokenRegex.Replace(pattern.MapReplace(tokenMap1).MapReplace(tokenMap2), "+");
        }

        public static bool DoesTopicMatchFilter(string topic, string filter)
        {
            return MqttTopicFilterComparer.Compare(topic, filter) == MqttTopicFilterCompareResult.IsMatch;
        }

        public static bool TryGetFieldValue(string pattern, string topic, string token, out string value)
        {
            int tokenPos = pattern.IndexOf(token, StringComparison.InvariantCulture);
            if (tokenPos < 0)
            {
                value = string.Empty;
                return false;
            }
            else
            {
                int depth = pattern.Substring(0, tokenPos).Count(c => c == '/');
                value = topic.Split('/')[depth];
                return true;
            }
        }

        /// <summary>
        /// Validates that a topic pattern is valid for use in a Command or Telemetry.
        /// </summary>
        /// <param name="pattern">The topic pattern.</param>
        /// <param name="residentTokenMap">An optional token replacement map, expected to last the lifetime of the calling class.</param>
        /// <param name="transientTokenMap">An optional token replacement map, expected to last for a single execution of a call tree.</param>
        /// <param name="requireReplacement">True if a replacement value is required for any token in the pattern.</param>
        /// <param name="errMsg">Out parameter to receive error message if validation fails.</param>
        /// <param name="errToken">Out parameter to receive the value of a missing or invalid token, if any.</param>
        /// <param name="errReplacement">Out parameter to receive the value of a missing or invalid replacement, if any.</param>
        /// <returns>A <see cref="PatternValidity"/> value indicating whether the pattern is valid or the way in which it is invalid.</returns>
        public static PatternValidity ValidateTopicPattern(
            string? pattern,
            IReadOnlyDictionary<string, string>? residentTokenMap,
            IReadOnlyDictionary<string, string>? transientTokenMap,
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
                    string token = level.Substring(1, level.Length - 2);
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
                    else if (residentTokenMap == null && transientTokenMap == null)
                    {
                        if (requireReplacement)
                        {
                            errMsg = $"Token '{level}' in MQTT topic pattern, but no replacement map provided";
                            errToken = token;
                            return PatternValidity.MissingReplacement;
                        }
                    }
                    else
                    {
                        string? residentReplacement = null;
                        string? transientReplacement = null;
                        bool hasResidentReplacement = residentTokenMap != null && residentTokenMap.TryGetValue(token, out residentReplacement);
                        bool hasTransientReplacement = transientTokenMap != null && transientTokenMap.TryGetValue(token, out transientReplacement);
                        if (!hasResidentReplacement && !hasTransientReplacement)
                        {
                            if (requireReplacement)
                            {
                                errMsg = $"Token '{level}' in MQTT topic pattern, but key '{token}' not found in replacement map";
                                errToken = token;
                                return PatternValidity.MissingReplacement;
                            }
                        }
                        else if (hasResidentReplacement && !IsValidReplacement(residentReplacement))
                        {
                            errMsg = $"Token '{level}' in MQTT topic pattern has resident replacement value '{residentReplacement}' that is not valid";
                            errToken = token;
                            errReplacement = residentReplacement;
                            return PatternValidity.InvalidResidentReplacement;
                        }
                        else if (hasTransientReplacement && !IsValidReplacement(transientReplacement))
                        {
                            errMsg = $"Token '{level}' in MQTT topic pattern has transient replacement value '{transientReplacement}' that is not valid";
                            errToken = token;
                            errReplacement = transientReplacement;
                            return PatternValidity.InvalidTransientReplacement;
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
                if (c < '!' || c > '~' || c == '+' || c == '#' || c == '{' || c == '}')
                {
                    return true;
                }
            }

            return false;
        }

        private static string MapReplace(this string pattern, IReadOnlyDictionary<string, string>? tokenMap)
        {
            if (tokenMap == null || !pattern.Contains('{'))
            {
                return pattern;
            }

            return replaceableTokenRegex.Replace(pattern, (Match match) => tokenMap.TryGetValue(match.Groups[1].Captures[0].Value, out string? value) ? value : match.Groups[0].Captures[0].Value);
        }
    }
}
