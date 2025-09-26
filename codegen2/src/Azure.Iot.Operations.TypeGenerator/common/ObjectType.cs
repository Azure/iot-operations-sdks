namespace Azure.Iot.Operations.TypeGenerator
{
    using System.Collections.Generic;
    using Azure.Iot.Operations.CodeGeneration;

    internal class ObjectType : SchemaType
    {
        internal override SchemaKind Kind { get => SchemaKind.Object; }

        internal ObjectType(CodeName schemaName, CodeName genNamespace, string? description, Dictionary<CodeName, FieldInfo> fieldInfos)
        {
            SchemaName = schemaName;
            Namespace = genNamespace;
            Description = description;
            FieldInfos = fieldInfos;
        }

        internal CodeName SchemaName { get; }

        internal CodeName Namespace { get; }

        internal string? Description { get; }

        internal Dictionary<CodeName, FieldInfo> FieldInfos { get; }

        internal class FieldInfo
        {
            internal FieldInfo(SchemaType schemaType, bool isIndirect, bool isRequired, string? description, int? index)
            {
                SchemaType = schemaType;
                IsIndirect = isIndirect;
                IsRequired = isRequired;
                Description = description;
                Index = index;
            }

            internal SchemaType SchemaType { get; }

            internal bool IsIndirect { get; }

            internal bool IsRequired { get; }

            internal string? Description { get; }

            internal int? Index { get; }
        }
    }
}
