﻿namespace Akri.Dtdl.Codegen
{
    using DTDLParser;
    using DTDLParser.Models;

    public class TopicCollisionDetector
    {
        private string topicType;
        private string topicProperty;
        private string nameToken;
        private bool combineWhenNoName;

        private Dictionary<string, Dictionary<string, Dtmi>> namedTopicInterfaceIds;
        private Dictionary<string, Dtmi> namelessTopicInterfaceIds;

        public static TopicCollisionDetector GetTelemetryTopicCollisionDetector() => new TopicCollisionDetector("Telemetry", DtdlMqttExtensionValues.TelemTopicProperty, MqttTopicTokens.TelemetryName, combineWhenNoName: true);
        public static TopicCollisionDetector GetCommandTopicCollisionDetector() => new TopicCollisionDetector("Command", DtdlMqttExtensionValues.CmdReqTopicProperty, MqttTopicTokens.CommandName, combineWhenNoName: false);

        public TopicCollisionDetector(string topicType, string topicProperty, string nameToken, bool combineWhenNoName)
        {
            this.topicType = topicType;
            this.topicProperty = topicProperty;
            this.nameToken = nameToken;
            this.combineWhenNoName = combineWhenNoName;

            namedTopicInterfaceIds = new();
            namelessTopicInterfaceIds = new();
        }

        public void Check(DTInterfaceInfo dtInterface, IEnumerable<string> names)
        {
            if (dtInterface.SupplementalProperties.TryGetValue(topicProperty, out object? topicObj) && topicObj is string topic)
            {
                if (!combineWhenNoName && !topic.Contains(nameToken) && names.Count() > 1)
                {
                    throw new Exception($"Interface {dtInterface.Id} has {topicType} topic \"{topic}\", which must include token \"{nameToken}\" because there is more than one {topicType}");
                }

                if (topic.Contains(MqttTopicTokens.ModelId))
                {
                    return;
                }

                if (topic.Contains(nameToken))
                {
                    namedTopicInterfaceIds.TryAdd(topic, new Dictionary<string, Dtmi>());
                    Dictionary<string, Dtmi> namedInterfaceIds = namedTopicInterfaceIds[topic];
                    foreach (string name in names)
                    {
                        if (!namedInterfaceIds.TryAdd(name, dtInterface.Id))
                        {
                            throw GetException(topic, dtInterface.Id, namedInterfaceIds[name], name);
                        }
                    }
                }
                else
                {
                    if (!namelessTopicInterfaceIds.TryAdd(topic, dtInterface.Id))
                    {
                        throw GetException(topic, dtInterface.Id, namelessTopicInterfaceIds[topic]);
                    }
                }
            }
        }

        private Exception GetException(string topic, Dtmi intfId1, Dtmi intfId2, string? name = null)
        {
            string subCondition = name != null ? $" and {topicType} with name \"{name}\"" : string.Empty;
            return new Exception($"Topic pattern collision -- Interfaces {intfId1} and {intfId2} both have {topicType} topic \"{topic}\"{subCondition}");
        }
    }
}
