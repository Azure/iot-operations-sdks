namespace Akri.Dtdl.Codegen
{
    using System.Collections.Generic;

    public class EnumType : SchemaType
    {
        public EnumType(string schemaName, string? description, string[] names, int[]? intValues = null, string[]? stringValues = null)
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

        public string SchemaName { get; }

        public string? Description { get; }

        public List<EnumValue> EnumValues { get; }

        public class EnumValue
        {
            public EnumValue(string name, int? intValue = null, string? stringValue = null)
            {
                Name = name;
                IntValue = intValue;
                StringValue = stringValue;
            }

            public string Name { get; }

            public int? IntValue { get; }

            public string? StringValue { get; }
        }
    }
}
