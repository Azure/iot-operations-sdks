namespace Azure.Iot.Operations.TypeGenerator
{
    using Azure.Iot.Operations.CodeGeneration;

    internal class ReferenceType : SchemaType
    {
        internal override SchemaKind Kind { get => SchemaKind.Reference; }

        internal ReferenceType(CodeName schemaName, CodeName genNamespace, bool isNullable = true, bool isEnum = false)
        {
            SchemaName = schemaName;
            Namespace = genNamespace;
            IsNullable = isNullable;
            IsEnum = isEnum;
        }

        internal CodeName SchemaName { get; }

        internal CodeName Namespace { get; }

        internal bool IsNullable { get; }

        internal bool IsEnum { get; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as ReferenceType);
        }

        internal bool Equals(ReferenceType? other)
        {
            return !ReferenceEquals(null, other) && SchemaName.Equals(other.SchemaName) && Namespace.Equals(other.Namespace);
        }

        public override int GetHashCode()
        {
            return SchemaName.GetHashCode() ^ Namespace.GetHashCode();
        }
    }
}
