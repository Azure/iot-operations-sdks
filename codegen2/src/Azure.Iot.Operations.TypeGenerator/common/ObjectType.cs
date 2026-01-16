namespace Azure.Iot.Operations.TypeGenerator
{
    using System.Collections.Generic;
    using Azure.Iot.Operations.CodeGeneration;

    internal class ObjectType : SchemaType
    {
        internal override SchemaKind Kind { get => SchemaKind.Object; }

        internal ObjectType(CodeName schemaName, string? description, Dictionary<CodeName, FieldInfo> fieldInfos, bool orNull)
            : base(orNull)
        {
            SchemaName = schemaName;
            Description = description;
            FieldInfos = fieldInfos;
        }

        internal CodeName SchemaName { get; }

        internal string? Description { get; }

        internal Dictionary<CodeName, FieldInfo> FieldInfos { get; }

        internal class FieldInfo
        {
            internal FieldInfo(SchemaType schemaType, bool isRequired, string? description)
            {
                IsIndirect = false;
                SchemaType = schemaType;
                IsRequired = isRequired;
                Description = description;
            }

            internal bool IsIndirect { get; set; }

            internal SchemaType SchemaType { get; }

            internal bool IsRequired { get; }

            internal string? Description { get; }
        }
    }
}
