// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;

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

            if (userProperties.Where(x => x.Name == name).Any())
            {
                value = userProperties.Where(x => x.Name == name).First().Value;
                return true;
            }

            return false;
        }
    }
}
