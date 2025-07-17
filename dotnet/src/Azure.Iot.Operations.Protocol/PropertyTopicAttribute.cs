// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Azure.Iot.Operations.Protocol
{
    [AttributeUsage(AttributeTargets.Class)]
    public class PropertyTopicAttribute(string topic) : Attribute
    {
        public string Topic { get; set; } = topic;
    }
}
