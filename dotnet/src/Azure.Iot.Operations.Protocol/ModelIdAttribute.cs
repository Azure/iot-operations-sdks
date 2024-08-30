namespace Azure.Iot.Operations.Protocol
{
    using System;

    [AttributeUsage(AttributeTargets.Class)]
    public class ModelIdAttribute : Attribute
    {
        public string Id { get; set; }

        public ModelIdAttribute(string id) => Id = id;
    }
}
