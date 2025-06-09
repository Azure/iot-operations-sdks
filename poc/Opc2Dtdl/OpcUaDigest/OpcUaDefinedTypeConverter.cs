namespace OpcUaDigest
{
    using System;
    using System.Collections.Generic;
    using YamlDotNet.Core;
    using YamlDotNet.Core.Events;
    using YamlDotNet.Serialization;

    public class OpcUaDefinedTypeConverter : IYamlTypeConverter
    {
        public bool Accepts(Type type) => type == typeof(OpcUaDefinedType);

        public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
        {
            return GetDefinedType(parser);
        }

        public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
        {
            throw new NotImplementedException();
        }

        private OpcUaDefinedType GetDefinedType(IParser parser)
        {
            if (!parser.TryConsume<SequenceStart>(out _))
            {
                throw new InvalidOperationException("Expected a YAML array for an OpcDefinedType");
            }

            OpcUaDefinedType definedType = InitDefinedType(parser);

            while (!parser.TryConsume<SequenceEnd>(out _))
            {
                if (!parser.TryConsume<MappingStart>(out _))
                {
                    throw new InvalidOperationException("Expected each OpcDefinedType array element to be a YAML object");
                }

                Scalar relationship = parser.Consume<Scalar>();

                if (relationship.Value == "UnitId")
                {
                    definedType.UnitId = parser.Consume<Scalar>().Value;
                }
                else if (relationship.Value == "Arguments")
                {
                    AddToDictionary(parser, definedType.Arguments, "Arguments");
                }
                else
                {
                    definedType.Contents.Add(new OpcUaContent(relationship.Value, GetDefinedType(parser)));
                }

                if (!parser.TryConsume<MappingEnd>(out _))
                {
                    throw new InvalidOperationException("Unexpected only one key/value pair as each element in OpcDefinedType content array");
                }
            }

            return definedType;
        }

        private OpcUaDefinedType InitDefinedType(IParser parser)
        {
            if (!parser.TryConsume<SequenceStart>(out _))
            {
                throw new InvalidOperationException("Expected a YAML array for an OpcDefinedType leading element");
            }

            string nodeType = parser.Consume<Scalar>().Value;
            string nodeId = parser.Consume<Scalar>().Value;
            string browseName = parser.Consume<Scalar>().Value;
            string? datatype = parser.TryConsume<Scalar>(out Scalar? scalar) ? scalar.Value : null;
            int valueRank = parser.TryConsume<Scalar>(out scalar) ? int.Parse(scalar.Value) : 0;
            int accessLevel = parser.TryConsume<Scalar>(out scalar) ? int.Parse(scalar.Value) : 0;

            if (!parser.TryConsume<SequenceEnd>(out _))
            {
                throw new InvalidOperationException("Unexpected element(s) at end of OpcDefinedType YAML array");
            }

            return new OpcUaDefinedType(nodeType, nodeId, browseName, datatype, valueRank, accessLevel);
        }

        private void AddToDictionary(IParser parser, Dictionary<string, (string?, int)> dict, string keyName)
        {
            if (!parser.TryConsume<MappingStart>(out _))
            {
                throw new InvalidOperationException($"Expected {keyName} value to be a YAML object");
            }

            while (!parser.TryConsume<MappingEnd>(out _))
            {
                Scalar key = parser.Consume<Scalar>();
                parser.Consume<SequenceStart>();
                string rawStringValue = parser.Consume<Scalar>().Value;
                string? stringValue = rawStringValue == "null" ? null : rawStringValue;
                int intValue = int.Parse(parser.Consume<Scalar>().Value);
                parser.Consume<SequenceEnd>();

                dict[key.Value] = (stringValue, intValue);
            }
        }
    }
}
