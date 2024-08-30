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
        private static readonly string[] RecognizedCommandTokens =
        {
            MqttTopicTokens.ModelId,
            MqttTopicTokens.CommandName,
            MqttTopicTokens.CommandExecutorId,
            MqttTopicTokens.CommandInvokerId,
        };

        private static readonly string[] RecognizedTelemetryTokens =
        {
            MqttTopicTokens.ModelId,
            MqttTopicTokens.TelemetryName,
            MqttTopicTokens.TelemetrySenderId,
        };

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
        /// Validates that a topic pattern is valid for use in a Command.
        /// </summary>
        /// <param name="pattern">The topic pattern.</param>
        /// <param name="paramName">The param name for the topic pattern.</param>
        /// <param name="commandName">The command name.</param>
        /// <param name="modelId">The model ID.</param>
        /// <param name="customTokenMap">An optional custom token map.</param>
        /// <exception cref="ArgumentException">Thrown if the topic pattern is invalid.</exception>"
        internal static void ValidateCommandTopicPattern(string? pattern, string paramName, string? commandName = default, string? modelId = default, Dictionary<string, string>? customTokenMap = default)
            => ValidateTopicPattern(pattern, RecognizedCommandTokens, paramName, commandName, modelId, customTokenMap, "Command");

        /// <summary>
        /// Validates that a topic pattern is valid for use in a Telemetry.
        /// </summary>
        /// <param name="pattern">The topic pattern.</param>
        /// <param name="paramName">The param name for the topic pattern.</param>
        /// <param name="telemetryName">The telemetry name.</param>
        /// <param name="modelId">The model ID.</param>
        /// <param name="customTokenMap">An optional custom token map.</param>
        /// <exception cref="ArgumentException">Thrown if the topic pattern is invalid.</exception>"
        public static void ValidateTelemetryTopicPattern(string? pattern, string paramName, string? telemetryName = default, string? modelId = default, Dictionary<string, string>? customTokenMap = default)
            => ValidateTopicPattern(pattern, RecognizedTelemetryTokens, paramName, telemetryName, modelId, customTokenMap, "Telemetry");

        /// <summary>
        /// Get an MQTT topic/filter string for a Command request or response.
        /// </summary>
        /// <param name="pattern">The pattern whose tokens to replace.</param>
        /// <param name="commandName">Value for the Command Name token; defaults to "+" for use in topic filters.</param>
        /// <param name="executorId">Value for the Command Executor ID token; defaults to "+" for use in topic filters.</param>
        /// <param name="invokerId">Value for the Command Invoker ID token; defaults to "+" for use in topic filters.</param>
        /// <param name="modelId">Value for the Model ID token; defaults to "+" for use in topic filters.</param>
        /// <param name="customTokenMap">A replacement map for replacing custom tokens in the provided pattern.</param>
        /// <returns><returns>The MQTT topic/filter for a command request or response.</returns>
        /// <exception cref="ArgumentException">Thrown if the topic pattern is null or empty.</exception></exception>
        public static string GetCommandTopic(string pattern, string commandName = "+", string executorId = "+", string invokerId = "+", string modelId = "+", Dictionary<string, string>? customTokenMap = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(pattern, nameof(pattern));

            if (pattern.Contains(MqttTopicTokens.CommandExecutorId) && executorId != "+")
            {
                ValidateTokenReplacement(MqttTopicTokens.CommandExecutorId, executorId, nameof(executorId));
            }

            return pattern
                .Replace(MqttTopicTokens.CommandName, commandName)
                .Replace(MqttTopicTokens.CommandExecutorId, executorId)
                .Replace(MqttTopicTokens.CommandInvokerId, invokerId)
                .Replace(MqttTopicTokens.ModelId, modelId)
                .MapReplace(customTokenMap);
        }

        /// <summary>
        /// Get an MQTT topic/filter string for a Telemetry.
        /// </summary>
        /// <param name="pattern">The pattern whose tokens to replace.</param>
        /// <param name="telemetryName">Value for the Name token; defaults to "+" for use in topic filters.</param>
        /// <param name="senderId">Value for the Telemetry Sender ID token; defaults to "+" for use in topic filters.</param>
        /// <param name="modelId">Value for the Model ID token; defaults to "+" for use in topic filters.</param>
        /// <param name="customTokenMap">A replacement map for replacing custom tokens in the provided pattern.</param>
        /// <returns>The MQTT topic/filter for a Telemtry.</returns>
        /// <exception cref="ArgumentException">Thrown if the topic pattern is null or empty.</exception></exception>
        public static string GetTelemetryTopic(string pattern, string? telemetryName = "+", string senderId = "+", string modelId = "+", Dictionary<string, string>? customTokenMap = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(pattern, nameof(pattern));

            return pattern
                .Replace(MqttTopicTokens.TelemetryName, telemetryName)
                .Replace(MqttTopicTokens.TelemetrySenderId, senderId)
                .Replace(MqttTopicTokens.ModelId, modelId)
                .MapReplace(customTokenMap);
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
        /// <param name="recognizedTokens">The collection of recognized tokens.</param>
        /// <param name="paramName">The param name for the topic pattern.</param>
        /// <param name="contentName">The command or telemetry name.</param>
        /// <param name="modelId">The model ID.</param>
        /// <param name="customTokenMap">An optional custom token map.</param>
        /// <param name="topicType">The topic type - command or telemetry.</param>
        /// <exception cref="ArgumentException">Thrown if the topic pattern is invalid.</exception>
        private static void ValidateTopicPattern(string? pattern, string[] recognizedTokens, string paramName, string? contentName, string? modelId, Dictionary<string, string>? customTokenMap, string topicType)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                throw new ArgumentException("MQTT topic pattern must not be empty", paramName);
            }

            if (pattern.StartsWith('$'))
            {
                throw new ArgumentException("MQTT topic pattern starts with reserved character '$'", paramName);
            }

            foreach (string level in pattern.Split('/'))
            {
                if (level.Length == 0)
                {
                    throw new ArgumentException("MQTT topic pattern contains empty level", paramName);
                }

                bool isToken = level.StartsWith('{') && level.EndsWith('}');
                if (isToken)
                {
                    bool isCustomToken = level.Substring(1).StartsWith(MqttTopicTokens.CustomPrefix, StringComparison.InvariantCulture);
                    if (isCustomToken)
                    {
                        string custom = level.Substring(MqttTopicTokens.CustomPrefix.Length + 1, level.Length - MqttTopicTokens.CustomPrefix.Length - 2);
                        if (custom.Length == 0)
                        {
                            throw new ArgumentException($"Custom token '{level}' in MQTT topic pattern is empty after '{MqttTopicTokens.CustomPrefix}' prefix", paramName);
                        }
                        else if (!custom.All(char.IsAsciiLetter))
                        {
                            throw new ArgumentException($"Custom token '{level}' in MQTT topic pattern must contain only ASCII letters after '{MqttTopicTokens.CustomPrefix}' prefix", paramName);
                        }
                        else if (customTokenMap == null)
                        {
                            throw new ArgumentException($"Custom token '{level}' in MQTT topic pattern, but no custom replacement map provided", paramName);
                        }
                        else if (!customTokenMap.TryGetValue(custom, out string? replacement))
                        {
                            throw new ArgumentException($"Custom token '{level}' in MQTT topic pattern, but key '{custom}' not found in custom replacement map", paramName);
                        }
                        else if (!IsValidReplacement(replacement))
                        {
                            throw new ArgumentException($"Custom token '{level}' in MQTT topic pattern has replatement value '{replacement}' that is not valid", paramName);
                        }
                    }
                    else
                    {
                        if (!recognizedTokens.Contains(level))
                        {
                            throw new ArgumentException($"Token '{level}' in MQTT topic pattern is not a recognized MQTT topic token for {topicType}", paramName);
                        }
                        else if (level == MqttTopicTokens.CommandName || level == MqttTopicTokens.TelemetryName)
                        {
                            ValidateTokenReplacement(level, contentName, paramName);
                        }
                        else if (level == MqttTopicTokens.ModelId)
                        {
                            ValidateTokenReplacement(level, modelId, paramName);
                        }
                    }
                }
                else
                {
                    if (ContainsInvalidChar(level))
                    {
                        throw new ArgumentException($"Level '{level}' in MQTT topic pattern contains invalid character", paramName);
                    }
                }
            }
        }

        /// <summary>
        /// Validates that a token replacement value is valid.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <param name="replacement">The replacement.</param>
        /// <param name="paramName">The param name for the topic pattern.</param>
        /// <exception cref="ArgumentException">Thrown if the replacement is null, empty or contains invalid characters.</exception>
        private static void ValidateTokenReplacement(string token, string? replacement, string paramName)
        {
            if (string.IsNullOrEmpty(replacement))
            {
                throw new ArgumentException($"MQTT topic pattern contains token '{token}', but no replacement value provided", paramName);
            }
            else if (ContainsInvalidChar(replacement) || replacement.Contains('/'))
            {
                throw new ArgumentException($"Token '{token}' in MQTT topic pattern has replacement value '{replacement}' that is not valid", paramName);
            }
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

        private static string MapReplace(this string pattern, Dictionary<string, string>? customTokenMap)
        {
            if (customTokenMap == null || !pattern.Contains('{'))
            {
                return pattern;
            }

            Regex rx = new($"{{{MqttTopicTokens.CustomPrefix}(\\w+)}}");

            return rx.Replace(pattern, (Match match) => customTokenMap[match.Groups[1].Captures[0].Value]);
        }
    }
}
