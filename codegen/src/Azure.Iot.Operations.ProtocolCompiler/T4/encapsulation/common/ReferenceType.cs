namespace Azure.Iot.Operations.ProtocolCompiler
{
    public class ReferenceType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Reference; }

        public ReferenceType(CodeName schemaName, CodeName genNamespace, bool isNullable = true, bool isEnum = false)
        {
            SchemaName = schemaName;
            Namespace = genNamespace;
            IsNullable = isNullable;
            IsEnum = isEnum;
        }

        public CodeName SchemaName { get; }

        public CodeName Namespace { get; }

        public bool IsNullable { get; }

        public bool IsEnum { get; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as ReferenceType);
        }

        public bool Equals(ReferenceType? other)
        {
            return !ReferenceEquals(null, other) && SchemaName.Equals(other.SchemaName) && Namespace.Equals(other.Namespace);
        }

        public override int GetHashCode()
        {
            return SchemaName.GetHashCode() ^ Namespace.GetHashCode();
        }
    }
}
