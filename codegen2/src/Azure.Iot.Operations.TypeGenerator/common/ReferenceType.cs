namespace Azure.Iot.Operations.TypeGenerator
{
    using Azure.Iot.Operations.CodeGeneration;

    internal class ReferenceType : SchemaType
    {
        internal override SchemaKind Kind { get => SchemaKind.Reference; }

        internal ReferenceType(CodeName schemaName, bool isNullable = true, bool isEnum = false)
        {
            SchemaName = schemaName;
            IsNullable = isNullable;
            IsEnum = isEnum;
        }

        internal CodeName SchemaName { get; }

        internal bool IsNullable { get; }

        internal bool IsEnum { get; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as ReferenceType);
        }

        internal bool Equals(ReferenceType? other)
        {
            return !ReferenceEquals(null, other) && SchemaName.Equals(other.SchemaName);
        }

        public override int GetHashCode()
        {
            return SchemaName.GetHashCode();
        }
    }
}
