// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Azure.Iot.Operations.Protocol
{
    [AttributeUsage(AttributeTargets.Class)]
    public class CommandTopicAttribute(string topic) : Attribute
    {
        public string RequestTopic { get; set; } = topic;
    }
}
