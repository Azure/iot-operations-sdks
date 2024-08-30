namespace Azure.Iot.Operations.Protocol
{
    using System;
    using System.Linq;

    internal static class AttributeRetriever
    {
        internal static bool HasAttribute<T>(Object obj)
            where T : Attribute
        {
            return GetAttribute<T>(obj) != null;
        }

        internal static T? GetAttribute<T>(Object obj)
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
