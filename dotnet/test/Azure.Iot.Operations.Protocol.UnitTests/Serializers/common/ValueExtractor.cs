// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

/* This file will be copied into the folder for generated code. */

namespace Azure.Iot.Operations.Protocol.UnitTests.Serializers.common
{
    using System;

    internal static class ValueExtractor
    {
#pragma warning disable IDE0030 // Null check can be simplified
        public static T Value<T>(this T obj) where T : class => obj;

        public static T Value<T>(this Nullable<T> val) where T : struct => val.HasValue ? val.Value : default(T);
#pragma warning restore IDE0030 // Null check can be simplified
    }
}
