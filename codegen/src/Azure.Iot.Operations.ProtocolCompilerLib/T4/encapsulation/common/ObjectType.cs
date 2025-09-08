namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    using System.Collections.Generic;

    public class ObjectType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Object; }

        public ObjectType(CodeName schemaName, CodeName genNamespace, string? description, Dictionary<CodeName, FieldInfo> fieldInfos)
        {
            SchemaName = schemaName;
            Namespace = genNamespace;
            Description = description;
            FieldInfos = fieldInfos;
        }

        public CodeName SchemaName { get; }

        public CodeName Namespace { get; }

        public string? Description { get; }

        public Dictionary<CodeName, FieldInfo> FieldInfos { get; }

        public class FieldInfo
        {
            public FieldInfo(SchemaType schemaType, bool isIndirect, bool isRequired, string? description, int? index)
            {
                SchemaType = schemaType;
                IsIndirect = isIndirect;
                IsRequired = isRequired;
                Description = description;
                Index = index;
            }

            public SchemaType SchemaType { get; }

            public bool IsIndirect { get; }

            public bool IsRequired { get; }

            public string? Description { get; }

            public int? Index { get; }
        }
    }
}
