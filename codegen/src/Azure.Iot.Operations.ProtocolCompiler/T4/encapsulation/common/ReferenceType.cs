namespace Azure.Iot.Operations.ProtocolCompiler
{
    public class ReferenceType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Reference; }

        public ReferenceType(string schemaName, bool isNullable = true, bool isEnum = false)
        {
            SchemaName = schemaName;
            IsNullable = isNullable;
            IsEnum = isEnum;
        }

        public string SchemaName { get; }

        public bool IsNullable { get; }

        public bool IsEnum { get; }
    }
}
