using System;

namespace Azure.Iot.Operations.Protocol
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ModelIdAttribute(string id) : Attribute
    {
        public string Id { get; set; } = id;
    }
}
