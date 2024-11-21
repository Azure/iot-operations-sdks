using System;

namespace Azure.Iot.Operations.Protocol
{
    [AttributeUsage(AttributeTargets.Class)]
    public class TelemetryTopicAttribute(string topic) : Attribute
    {
        public string Topic { get; set; } = topic;
    }
}
