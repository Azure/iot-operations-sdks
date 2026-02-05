// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Azure.Iot.Operations.Protocol
{
    internal static class MqttUserPropertyListExtension
    {
        internal static bool TryGetProperty(this List<Azure.Iot.Operations.Protocol.Models.MqttUserProperty> userProperties, string name, out string? value)
        {
            value = default;
            if (userProperties == null)
            {
                return false;
            }

            var property = userProperties.FirstOrDefault(x => x.Name == name);
            if (property != null)
            {
                value = property.Value;
                return true;
            }

            return false;
        }

        internal static bool TryGetPropertyBuffer(this List<Azure.Iot.Operations.Protocol.Models.MqttUserProperty> userProperties, string name, out ReadOnlyMemory<byte> value)
        {
            value = default;
            if (userProperties == null)
            {
                return false;
            }

            var property = userProperties.FirstOrDefault(x => x.Name == name);
            if (property != null)
            {
                value = property.ValueBuffer;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Extension methods for reading MqttUserProperty values.
    /// </summary>
    public static class MqttUserPropertyExtensions
    {
        /// <summary>
        /// Reads the value of the user property as a UTF-8 string.
        /// </summary>
        /// <param name="userProperty">The user property to read.</param>
        /// <returns>The value as a string.</returns>
        public static string ReadValueAsString(this Azure.Iot.Operations.Protocol.Models.MqttUserProperty userProperty)
        {
            ArgumentNullException.ThrowIfNull(userProperty);

            var buffer = userProperty.ValueBuffer;
            if (buffer.IsEmpty)
            {
                return string.Empty;
            }

            return Encoding.UTF8.GetString(buffer.Span);
        }
    }
}
