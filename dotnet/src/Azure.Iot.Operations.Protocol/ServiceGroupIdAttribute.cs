namespace Azure.Iot.Operations.Protocol
{
    using System;

    [AttributeUsage(AttributeTargets.Class)]
    public class ServiceGroupIdAttribute : Attribute
    {
        public string Id { get; set; }

        public ServiceGroupIdAttribute(string id) => Id = id;
    }
}
