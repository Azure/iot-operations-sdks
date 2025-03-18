// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Azure.Iot.Operations.Protocol.Models
{
    public class MqttUserProperty(string name, string value)
    {
        public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

        public string Value { get; } = value ?? throw new ArgumentNullException(nameof(value));
    }
}