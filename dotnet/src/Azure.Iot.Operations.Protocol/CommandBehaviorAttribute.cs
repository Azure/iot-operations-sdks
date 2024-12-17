// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Azure.Iot.Operations.Protocol
{
    [AttributeUsage(AttributeTargets.Class)]
    public class CommandBehaviorAttribute(bool idempotent = false, string cacheTtl = "PT0H0M0S") : Attribute
    {
        public bool IsIdempotent { get; set; } = idempotent;

        public string CacheTtl { get; set; } = cacheTtl;
    }
}
