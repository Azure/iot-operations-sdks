namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Collections.Generic;

    public class EnumType : SchemaType
    {
        public override SchemaKind Kind { get => EnumValues.FirstOrDefault()?.StringValue != null ? SchemaKind.EnumString : SchemaKind.EnumInt; }

        public EnumType(CodeName schemaName, string? description, CodeName[] names, int[]? intValues = null, string[]? stringValues = null)
        {
            SchemaName = schemaName;
            Description = description;
            EnumValues = new();

            for (int ix = 0; ix < names.Length; ++ix)
            {
                int? intValue = intValues != null ? intValues[ix] : null;
                string? stringValue = stringValues != null ? stringValues[ix] : null;
                EnumValues.Add(new EnumValue(names[ix], intValue, stringValue));
            }
        }

        public CodeName SchemaName { get; }

        public string? Description { get; }

        public List<EnumValue> EnumValues { get; }

        public class EnumValue
        {
            public EnumValue(CodeName name, int? intValue = null, string? stringValue = null)
            {
                Name = name;
                IntValue = intValue;
                StringValue = stringValue;
            }

            public CodeName Name { get; }

            public int? IntValue { get; }

            public string? StringValue { get; }
        }
    }
}
