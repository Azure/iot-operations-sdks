using Azure.Iot.Operations.Protocol.Models;
using System.Text.RegularExpressions;

namespace Azure.Iot.Operations.Services.StateStore
{
    //TODO this code is temporary while the telemetry receiver pattern is implemented in code gen. Once it is implemented
    // in code gen, this should be handled by the underlying library and this block can be deleted.

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
        /// <param name="tokenMap1">A first optional token replacement map.</param>
        /// <param name="tokenMap2">A second optional token replacement map.</param>
        /// <param name="requireReplacement">True if a replacement value is required for any token in the pattern.</param>
        /// <param name="errMsg">Out parameter to receive error message if validation fails.</param>
        /// <returns>True if pattern is valid; false otherwise.</returns>
        public static bool TryValidateTopicPattern(
            string? pattern,
            IReadOnlyDictionary<string, string>? tokenMap1,
            IReadOnlyDictionary<string, string>? tokenMap2,
            bool requireReplacement,
            out string errMsg)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                errMsg = "MQTT topic pattern must not be empty";
                return false;
            }

            if (pattern.StartsWith('$'))
            {
                errMsg = "MQTT topic pattern starts with reserved character '$'";
                return false;
            }

            foreach (string level in pattern.Split('/'))
            {
                if (level.Length == 0)
                {
                    errMsg = "MQTT topic pattern contains empty level";
                    return false;
                }

                bool isToken = level.StartsWith('{') && level.EndsWith('}');
                if (isToken)
                {
                    string token = level.Substring(1, level.Length - 2);
                    if (token.Length == 0)
                    {
                        errMsg = "Token in MQTT topic pattern is empty";
                        return false;
                    }
                    else if (ContainsInvalidChar(token))
                    {
                        errMsg = $"Token '{level}' in MQTT topic pattern contains invalid character";
                        return false;
                    }
                    else if (tokenMap1 == null && tokenMap2 == null)
                    {
                        if (requireReplacement)
                        {
                            errMsg = $"Token '{level}' in MQTT topic pattern, but no replacement map provided";
                            return false;
                        }
                    }
                    else if ((tokenMap1 == null || !tokenMap1.TryGetValue(token, out string? replacement))
                        && (tokenMap2 == null || !tokenMap2.TryGetValue(token, out replacement)))
                    {
                        if (requireReplacement)
                        {
                            errMsg = $"Token '{level}' in MQTT topic pattern, but key '{token}' not found in replacement map";
                            return false;
                        }
                    }
                    else if (!IsValidReplacement(replacement))
                    {
                        errMsg = $"Token '{level}' in MQTT topic pattern has replatement value '{replacement}' that is not valid";
                        return false;
                    }
                }
                else
                {
                    if (ContainsInvalidChar(level))
                    {
                        errMsg = $"Level '{level}' in MQTT topic pattern contains invalid character";
                        return false;
                    }
                }
            }

            errMsg = string.Empty;
            return true;
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
