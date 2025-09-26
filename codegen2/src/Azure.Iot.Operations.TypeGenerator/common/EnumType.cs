namespace Azure.Iot.Operations.TypeGenerator
{
    using Azure.Iot.Operations.CodeGeneration;

    internal class EnumType : SchemaType
    {
        internal override SchemaKind Kind { get => SchemaKind.Enum; }

        internal EnumType(CodeName schemaName, CodeName genNamespace, string? description, CodeName[] enumValues)
        {
            SchemaName = schemaName;
            Namespace = genNamespace;
            Description = description;
            EnumValues = enumValues;
        }

        internal CodeName SchemaName { get; }

        internal CodeName Namespace { get; }

        internal string? Description { get; }

        internal CodeName[] EnumValues { get; }
    }
}
