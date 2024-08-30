namespace Akri.Dtdl.Codegen
{
    using System.Collections.Generic;

    public class ObjectType : SchemaType
    {
        public ObjectType(string schemaName, string? description, Dictionary<string, FieldInfo> fieldInfos)
        {
            SchemaName = schemaName;
            Description = description;
            FieldInfos = fieldInfos;
        }

        public string SchemaName { get; }

        public string? Description { get; }

        public Dictionary<string, FieldInfo> FieldInfos { get; }

        public class FieldInfo
        {
            public FieldInfo(SchemaType schemaType, bool isRequired, string? description, int? index)
            {
                SchemaType = schemaType;
                IsRequired = isRequired;
                Description = description;
                Index = index;
            }

            public SchemaType SchemaType { get; }

            public bool IsRequired { get; }

            public string? Description { get; }

            public int? Index { get; }
        }
    }
}
