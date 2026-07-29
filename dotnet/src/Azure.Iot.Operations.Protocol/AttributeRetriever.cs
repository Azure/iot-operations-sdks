// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;

namespace Azure.Iot.Operations.Protocol
{
    public static class AttributeRetriever
    {
        public static bool HasAttribute<T>(object obj)
            where T : Attribute
        {
            return GetAttribute<T>(obj) != null;
        }

        public static T? GetAttribute<T>(object obj)
            where T : Attribute
        {
            T? attr = null;
            Type? type = obj.GetType();

            while (attr == null && type != null)
            {
                attr = type.GetCustomAttributes(true).OfType<T>().FirstOrDefault();
                type = type.DeclaringType;
            }

            return attr;
        }
    }
}
