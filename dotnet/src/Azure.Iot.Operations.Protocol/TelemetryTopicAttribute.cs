namespace Azure.Iot.Operations.Protocol
{
    using System;

    [AttributeUsage(AttributeTargets.Class)]
    public class TelemetryTopicAttribute : Attribute
    {
        public string Topic { get; set; }

        public TelemetryTopicAttribute(string topic) => Topic = topic;
    }
}
