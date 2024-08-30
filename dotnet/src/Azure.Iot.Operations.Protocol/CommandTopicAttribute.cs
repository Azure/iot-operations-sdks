namespace Azure.Iot.Operations.Protocol
{
    using System;

    [AttributeUsage(AttributeTargets.Class)]
    public class CommandTopicAttribute : Attribute
    {
        public string RequestTopic { get; set; }

        public CommandTopicAttribute(string topic) => RequestTopic = topic;
    }
}
