namespace OpcUaDigest
{
    using System;
    using YamlDotNet.Core;
    using YamlDotNet.Core.Events;
    using YamlDotNet.Serialization;

    public class StringIntTupleTypeConverter : IYamlTypeConverter
    {
        public bool Accepts(Type type) => type == typeof((string?, int));

        public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
        {
            if (!parser.TryConsume<SequenceStart>(out _))
            {
                throw new InvalidOperationException("Expected a YAML array for a tuple");
            }

            string rawStringValue = parser.Consume<Scalar>().Value;
            string? stringValue = rawStringValue == "null" ? null : rawStringValue;
            int intValue = int.Parse(parser.Consume<Scalar>().Value);

            if (!parser.TryConsume<SequenceEnd>(out _))
            {
                throw new InvalidOperationException("Unexpected element(s) at end of tuple YAML array");
            }

            return (stringValue, intValue);
        }

        public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
