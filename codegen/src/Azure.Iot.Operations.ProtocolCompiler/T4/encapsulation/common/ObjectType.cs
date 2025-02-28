namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Collections.Generic;

    public class ObjectType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Object; }

        public ObjectType(CodeName schemaName, string? description, Dictionary<CodeName, FieldInfo> fieldInfos)
        {
            SchemaName = schemaName;
            Description = description;
            FieldInfos = fieldInfos;
        }

        public CodeName SchemaName { get; }

        public string? Description { get; }

        public Dictionary<CodeName, FieldInfo> FieldInfos { get; }

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
