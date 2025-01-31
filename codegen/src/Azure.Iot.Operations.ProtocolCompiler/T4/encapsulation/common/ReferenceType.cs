namespace Azure.Iot.Operations.ProtocolCompiler
{
    public class ReferenceType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Reference; }

        public ReferenceType(CodeName schemaName, bool isNullable = true, bool isEnum = false)
        {
            SchemaName = schemaName;
            IsNullable = isNullable;
            IsEnum = isEnum;
        }

        public CodeName SchemaName { get; }

        public bool IsNullable { get; }

        public bool IsEnum { get; }
    }
}
