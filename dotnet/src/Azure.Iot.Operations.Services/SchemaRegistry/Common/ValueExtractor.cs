// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

namespace Azure.Iot.Operations.Services.SchemaRegistry
{
    using System;

    internal static class ValueExtractor
    {
#pragma warning disable IDE0030 // Null check can be simplified
        public static T Value<T>(this T obj)
            where T : class => obj;

        public static T Value<T>(this T? val)
            where T : struct => val.HasValue ? val.Value : default(T);
#pragma warning restore IDE0030 // Null check can be simplified
    }
}
