namespace Azure.Iot.Operations.TypeGenerator
{
    using Azure.Iot.Operations.CodeGeneration;

    internal class AliasType : SchemaType
    {
        internal override SchemaKind Kind { get => SchemaKind.Alias; }

        internal AliasType(CodeName schemaName, string? description, CodeName referencedName)
        {
            SchemaName = schemaName;
            Description = description;
            ReferencedName = referencedName;
        }

        internal CodeName SchemaName { get; }

        internal string? Description { get; }

        internal CodeName ReferencedName { get; }
    }
}
