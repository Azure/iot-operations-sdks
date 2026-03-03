// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

/* This file will be copied into the folder for generated code. */

using System;

namespace Azure.Iot.Operations.Protocol.UnitTests.Serializers.common
{
    [AttributeUsage(AttributeTargets.Class)]
    public class PropertyTopicAttribute(string topic) : Attribute
    {
        public string Topic { get; set; } = topic;
    }
}
